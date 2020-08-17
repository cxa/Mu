namespace Mu

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Reflection
open System.Threading
open FSharp.Quotations.Evaluator

type Send<'msg> = 'msg -> unit

type IView<'model, 'msg> =
  abstract BindModel: 'model -> Expr<unit>
  abstract BindMsg: Send<'msg> -> unit

type Cmd<'msg> = Cmd'<'msg> list
and internal Cmd'<'msg> = Send<'msg> -> unit

[<RequireQualifiedAccess>]
module Cmd =
  let internal exec send (cmd: Cmd<'msg>) = cmd |> List.iter (fun cmd -> cmd send)
  let internal isNone (cmd: Cmd<'msg>) = List.isEmpty cmd

  let none: Cmd<'msg> = []
  let map (f: 'a -> 'msg) (cmd: Cmd<'a>) : Cmd<'msg> = cmd |> List.map (fun g -> (fun send -> f >> send) >> g)
  let batch (cmds: #seq<Cmd<'msg>>) : Cmd<'msg> = cmds |> List.concat

  let ofMsg (msg:'msg): Cmd<'msg> = [fun send -> send msg]

  module OfFunc =
    let either (func: unit -> 'a) (ofSuccess: 'a -> 'msg) (ofError: exn -> 'msg) =
      let msg =
        try ofSuccess (func ())
        with x -> ofError x
      ofMsg msg

    let perform (func: unit -> 'a) (ofSuccess: 'a -> 'msg) : Cmd<'msg> =
      let cmd send =
        try func () |> ofSuccess |> send
        with _ -> ()
      [cmd]

    let attempt (func: unit -> _) (ofError: exn -> 'msg) : Cmd<'msg> =
      let cmd send =
        try func () |> ignore
        with x -> ofError x |> send
      [cmd]

    let eff (func: unit -> _) : Cmd<'msg> =
      try func () |> ignore
      with _ -> ()
      none

  module OfAsync =
    let either' (computation: Async<'a>) (ofSuccess: 'a -> 'msg) (ofError: exn -> 'msg) (cTokenSrc: CancellationTokenSource) : Cmd<'msg> =
      let cmd send =
        let async = async {
          let! choice = Async.Catch computation
          let! token = Async.CancellationToken
          if not token.IsCancellationRequested then
            let msg = match choice with
                      | Choice1Of2 a -> ofSuccess a
                      | Choice2Of2 e -> ofError e
            send msg
        }
        match isNull cTokenSrc with
        | true -> Async.Start (async)
        | false -> Async.Start (async, cTokenSrc.Token)
      [cmd]

    let either (computation: Async<'a>) (ofSuccess: 'a -> 'msg) (ofError: exn -> 'msg) =
      either' computation ofSuccess ofError null

    let perform' (computation: Async<'a>) (ofSuccess: 'a -> 'msg) (cTokenSrc: CancellationTokenSource) : Cmd<'msg> =
      let cmd send =
        let async = async {
          let! choice = Async.Catch computation
          match choice with
          | Choice1Of2 a -> send (ofSuccess a)
          | Choice2Of2 _ -> ()
        }
        match isNull cTokenSrc with
        | true -> Async.Start (async)
        | false -> Async.Start (async, cTokenSrc.Token)
      [cmd]

    let perform (computation: Async<'a>) (ofSuccess: 'a -> 'msg) =
      perform' computation ofSuccess null

    let attempt' (computation: Async<'a>) (ofError: exn -> 'msg) (cTokenSrc: CancellationTokenSource) : Cmd<'msg> =
      let cmd send =
        let async = async {
          let! choice = Async.Catch computation
          match choice with
          | Choice1Of2 _ -> ()
          | Choice2Of2 e -> send (ofError e)
        }
        match isNull cTokenSrc with
        | true -> Async.Start (async)
        | false -> Async.Start (async, cTokenSrc.Token)
      [cmd]

    let attempt (computation: Async<'a>) (ofError: exn -> 'msg) =
      attempt' computation ofError null

  module OfTask =
    let either' (task: Tasks.Task<'a>) (ofSuccess: 'a -> 'msg) (ofError: exn -> 'msg) (cTokenSrc: CancellationTokenSource) =
      OfAsync.either' (Async.AwaitTask task) ofSuccess ofError cTokenSrc

    let either (task: Tasks.Task<'a>) (ofSuccess: 'a -> 'msg) (ofError: exn -> 'msg) =
      OfAsync.either' (Async.AwaitTask task) ofSuccess ofError null

    let perform' (task: Tasks.Task<'a>) (ofSuccess: 'a -> 'msg) (cTokenSrc: CancellationTokenSource) =
      OfAsync.perform' (Async.AwaitTask task) ofSuccess cTokenSrc

    let perform (task: Tasks.Task<'a>) (ofSuccess: 'a -> 'msg) =
      OfAsync.perform' (Async.AwaitTask task) ofSuccess null

    let attempt' (task: Tasks.Task<'a>) (ofError: exn -> 'msg) (cTokenSrc: CancellationTokenSource) =
      OfAsync.attempt' (Async.AwaitTask task) ofError cTokenSrc

    let attempt (task: Tasks.Task<'a>) (ofError: exn -> 'msg) =
      OfAsync.attempt' (Async.AwaitTask task) ofError null

type private ModelEventHandler<'model> (uiContext: SynchronizationContext) =
  let rec splitExpr =
    function
    | Sequential(h, t) -> h :: splitExpr t
    | t -> [ t ]

  let rec exprContainsFields fields expr =
    let thatExpr = exprContainsFields fields
    match expr with
    | Application(e1, e2) -> thatExpr e1 || thatExpr e2
    | Call(eOpt, _methodInfo, eList) ->
      (eOpt |> Option.map thatExpr |> Option.defaultValue false) || (eList |> List.exists thatExpr)
    | Coerce(e, _) -> thatExpr e
    | FieldGet(Some(e), _) -> thatExpr e
    | FieldSet(Some(e), _, e2) -> thatExpr e || thatExpr e2
    | ForIntegerRangeLoop(_, e, e2, e3) -> thatExpr e || thatExpr e2 || thatExpr e3
    | IfThenElse(e, e2, e3) -> thatExpr e || thatExpr e2 || thatExpr e3
    | Lambda(_, e) -> thatExpr e
    | Let(_, e1, e2) -> thatExpr e1 || thatExpr e2
    | LetRecursive(el, e2) -> thatExpr e2 || el |> List.exists (fun (_, e) -> thatExpr e)
    | NewArray(_, el) -> el |> List.exists thatExpr
    | NewDelegate(_, _, e) -> thatExpr e
    | NewObject(_, el) -> el |> List.exists thatExpr
    | NewRecord(_, el) -> el |> List.exists thatExpr
    | NewTuple(el) -> el |> List.exists thatExpr
    | NewUnionCase(_, el) -> el |> List.exists thatExpr
    | PropertySet(_, _, _, e) -> thatExpr e
    | PropertyGet(Some(Value(o, _)), propInfo, []) when (o :? 'model) -> Set.contains propInfo.Name fields
    | TryFinally(e, e2) -> thatExpr e || thatExpr e2
    | TryWith(e, _, e2, _, e3) -> thatExpr e || thatExpr e2 || thatExpr e3
    | TupleGet(e, _) -> thatExpr e
    | TypeTest(e, _) -> thatExpr e
    | UnionCaseTest(e, _) -> thatExpr e
    | VarSet(_, e) -> thatExpr e
    | WhileLoop(e, e2) -> thatExpr e || thatExpr e2
    | _ -> false

  let event =
    let evt = Event<Set<string> * Expr>() // keys * expr
    evt.Publish.Add(fun (changedFields, expr) ->
     expr
     |> splitExpr
     |> List.filter (exprContainsFields changedFields)
     |> List.iter (fun e ->
      // ensure perform UI updates on UI thread 
      uiContext.Post ((fun _ -> QuotationEvaluator.EvaluateUntyped e |> ignore), null)))
    evt

  member __.NotifyChange fields expr = event.Trigger(fields, expr)

[<RequireQualifiedAccess>]
module Mu =
  type T<'model, 'msg> =
    { Init: unit -> 'model
      Update: 'model -> 'msg -> ('model * Cmd<'msg>)
      View: IView<'model, 'msg> }

  let private diff fields m1 m2 expr callback =
    let diffFields =
      fields
      |> Seq.fold (fun acc (field: System.Reflection.PropertyInfo) ->
        let v1, v2 = field.GetValue m1, field.GetValue m2
        if v1 <> v2 then Set.add field.Name acc else acc) Set.empty
    if not (Set.isEmpty diffFields) then callback diffFields expr

  // should always run in UI thread
  let run' (t: T<'model, 'msg>) =
    let { T.Init = init; Update = update; View = view } = t
    let uiSyncContext =
      let ctx = SynchronizationContext.Current
      if isNull ctx then
        failwith "Can't get UI SynchronizationContext, make sure you run Mu afer UI application initialized"
      ctx

    let model = init () |> ref
    let modelEventHandler = new ModelEventHandler<'model> (uiSyncContext)
    let msgEventHandler = Event<'msg> ()
    let sendMsg msg = msgEventHandler.Trigger msg
    let fields = FSharpType.GetRecordFields((!model).GetType())
    msgEventHandler.Publish.Add(fun msg ->
      let newModel, cmd = update !model msg
      if !model <> newModel then
        diff fields !model newModel (view.BindModel newModel) modelEventHandler.NotifyChange
        model := newModel
      if not (Cmd.isNone cmd) then Cmd.exec sendMsg cmd
    )
    view.BindModel !model |> QuotationEvaluator.Evaluate
    view.BindMsg sendMsg

  let run init update view =
    run' { Init = init; Update = update; View = view }
