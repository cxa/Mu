namespace Mu

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open System.Threading
open FSharp.Quotations.Evaluator

type Send<'msg> = 'msg -> unit

type IView<'model, 'msg> =
  abstract BindModel : 'model -> Expr<unit>
  abstract BindMsg : Send<'msg> -> unit

type Cmd<'msg> = Cmd'<'msg> list

and private Cmd'<'msg> = Send<'msg> -> unit

module private UIContext =
  let mutable current : SynchronizationContext = null
  let run (fn: unit -> unit) = current.Post((fun _ -> fn ()), null)

[<RequireQualifiedAccess>]
module Cmd =
  let internal exec send (cmd: Cmd<'msg>) = cmd |> List.iter (fun cmd -> cmd send)
  let internal isNone (cmd: Cmd<'msg>) = List.isEmpty cmd
  let private startAsync = Async.Start

  let none : Cmd<'msg> = []

  let map (f: 'a -> 'msg) (cmd: Cmd<'a>) : Cmd<'msg> =
    cmd |> List.map (fun g -> (fun send -> f >> send) >> g)

  let batch (cmds: #seq<Cmd<'msg>>) : Cmd<'msg> = cmds |> List.concat

  let ofMsg (msg: 'msg) : Cmd<'msg> = [ fun send -> send msg ]

  module OfFunc =
    let either (fn: unit -> 'a) (ofSuccess: 'a -> 'msg) (ofError: exn -> 'msg) =
      let msg =
        try
          ofSuccess (fn ())
        with x -> ofError x
      ofMsg msg

    let perform (fn: unit -> 'a) (ofSuccess: 'a -> 'msg) : Cmd<'msg> =
      let cmd send =
        try
          fn () |> ofSuccess |> send
        with _ -> ()
      [ cmd ]

    let attempt (fn: unit -> _) (ofError: exn -> 'msg) : Cmd<'msg> =
      let cmd send =
        try
          fn () |> ignore
        with x -> ofError x |> send
      [ cmd ]

  module OfEff =
    type RunOnUIThread = (unit -> unit) -> unit

    let run (fn: RunOnUIThread -> unit) =
      try
        fn UIContext.run |> ignore
      with _ -> ()
      none

    let ui (fn: unit -> unit) =
      try
        UIContext.run fn
      with _ -> ()
      none

    let just (fn: unit -> unit) =
      try
        fn () |> ignore
      with _ -> ()
      none

  module OfAsync' =
    let either
      (start: Async<unit> -> unit)
      (computation: Async<'a>)
      (ofSuccess: 'a -> 'msg)
      (ofError: exn -> 'msg)
      : Cmd<'msg>
      =
      let cmd send =
        async {
          let! token = Async.CancellationToken
          if token.IsCancellationRequested then return ()
          let! choice = Async.Catch computation
          if token.IsCancellationRequested then return ()
          let msg =
            match choice with
            | Choice1Of2 a -> ofSuccess a
            | Choice2Of2 e -> ofError e
          send msg
        }
      [ cmd >> start ]

    let perform
      (start: Async<unit> -> unit)
      (computation: Async<'a>)
      (ofSuccess: 'a -> 'msg)
      : Cmd<'msg>
      =
      let cmd send =
        async {
          let! choice = Async.Catch computation
          match choice with
          | Choice1Of2 a -> send (ofSuccess a)
          | Choice2Of2 _ -> ()
        }
      [ cmd >> start ]

    let attempt
      (start: Async<unit> -> unit)
      (computation: Async<'a>)
      (ofError: exn -> 'msg)
      : Cmd<'msg>
      =
      let cmd send =
        async {
          let! choice = Async.Catch computation
          match choice with
          | Choice1Of2 _ -> ()
          | Choice2Of2 e -> send (ofError e)
        }
      [ cmd >> start ]

  module OfAsync =
    let either (computation: Async<'a>) (ofSuccess: 'a -> 'msg) (ofError: exn -> 'msg) =
      OfAsync'.either startAsync computation ofSuccess ofError

    let perform (computation: Async<'a>) (ofSuccess: 'a -> 'msg) =
      OfAsync'.perform startAsync computation ofSuccess

    let attempt (computation: Async<'a>) (ofError: exn -> 'msg) =
      OfAsync'.attempt startAsync computation ofError

  module OfTask' =
    let either
      (start: Async<unit> -> unit)
      (task: Tasks.Task<'a>)
      (ofSuccess: 'a -> 'msg)
      (ofError: exn -> 'msg)
      =
      OfAsync'.either start (Async.AwaitTask task) ofSuccess ofError

    let perform (start: Async<unit> -> unit) (task: Tasks.Task<'a>) (ofSuccess: 'a -> 'msg) =
      OfAsync'.perform start (Async.AwaitTask task) ofSuccess

    let attempt (start: Async<unit> -> unit) (task: Tasks.Task<'a>) (ofError: exn -> 'msg) =
      OfAsync'.attempt start (Async.AwaitTask task) ofError

  module OfTask =
    let either (task: Tasks.Task<'a>) (ofSuccess: 'a -> 'msg) (ofError: exn -> 'msg) =
      OfAsync'.either startAsync (Async.AwaitTask task) ofSuccess ofError

    let perform (task: Tasks.Task<'a>) (ofSuccess: 'a -> 'msg) =
      OfAsync'.perform startAsync (Async.AwaitTask task) ofSuccess

    let attempt (task: Tasks.Task<'a>) (ofError: exn -> 'msg) =
      OfAsync'.attempt startAsync (Async.AwaitTask task) ofError

type private ModelEventHandler<'model>() =
  let rec splitExpr =
    function
    | Sequential (h, t) -> h :: splitExpr t
    | t -> [ t ]

  let rec exprContainsProps props expr =
    let thatExpr = exprContainsProps props
    match expr with
    | Application (e1, e2) -> thatExpr e1 || thatExpr e2
    | Call (eOpt, _methodInfo, eList) ->
      (eOpt |> Option.map thatExpr |> Option.defaultValue false)
      || (eList |> List.exists thatExpr)
    | Coerce (e, _) -> thatExpr e
    | FieldGet (Some (e), _) -> thatExpr e
    | FieldSet (Some (e), _, e2) -> thatExpr e || thatExpr e2
    | ForIntegerRangeLoop (_, e, e2, e3) -> thatExpr e || thatExpr e2 || thatExpr e3
    | IfThenElse (e, e2, e3) -> thatExpr e || thatExpr e2 || thatExpr e3
    | Lambda (_, e) -> thatExpr e
    | Let (_, e1, e2) -> thatExpr e1 || thatExpr e2
    | LetRecursive (el, e2) -> thatExpr e2 || el |> List.exists (fun (_, e) -> thatExpr e)
    | NewArray (_, el) -> el |> List.exists thatExpr
    | NewDelegate (_, _, e) -> thatExpr e
    | NewObject (_, el) -> el |> List.exists thatExpr
    | NewRecord (_, el) -> el |> List.exists thatExpr
    | NewTuple (el) -> el |> List.exists thatExpr
    | NewUnionCase (_, el) -> el |> List.exists thatExpr
    | PropertySet (_, _, _, e) -> thatExpr e
    | PropertyGet (Some (Value (o, _)), propInfo, []) when (o :? 'model) ->
      Set.contains propInfo.Name props
    | TryFinally (e, e2) -> thatExpr e || thatExpr e2
    | TryWith (e, _, e2, _, e3) -> thatExpr e || thatExpr e2 || thatExpr e3
    | TupleGet (e, _) -> thatExpr e
    | TypeTest (e, _) -> thatExpr e
    | UnionCaseTest (e, _) -> thatExpr e
    | VarSet (_, e) -> thatExpr e
    | WhileLoop (e, e2) -> thatExpr e || thatExpr e2
    | _ -> false

  let event =
    let evt = Event<Set<string> * Expr>() // keys * expr
    evt.Publish.Add(fun (changedProps, expr) ->
      expr
      |> splitExpr
      |> List.filter (exprContainsProps changedProps)
      |> List.iter (fun e ->
        UIContext.run (fun () -> QuotationEvaluator.EvaluateUntyped e |> ignore)
      )
    )
    evt

  member __.NotifyChange props expr = event.Trigger(props, expr)

[<RequireQualifiedAccess>]
module Mu =
  type T<'model, 'msg> =
    { Init: unit -> 'model * Cmd<'msg>
      Update: 'model -> 'msg -> 'model * Cmd<'msg>
      View: IView<'model, 'msg> }

  let private diff props m1 m2 expr callback =
    let diffProps =
      props
      |> Seq.fold
        (fun acc (prop: System.Reflection.PropertyInfo) ->
          let v1, v2 = prop.GetValue m1, prop.GetValue m2
          if v1 <> v2 then Set.add prop.Name acc else acc
        )
        Set.empty
    if not (Set.isEmpty diffProps) then callback diffProps expr

  // should always run in UI thread
  let run' (t: T<'model, 'msg>) =
    let { T.Init = init; Update = update; View = view } = t
    if isNull UIContext.current then
      let ctx = SynchronizationContext.Current
      if isNull ctx then
        failwith
          "Can't get UI SynchronizationContext, make sure you run Mu afer UI application initialized"
      UIContext.current <- ctx

    let initModel, initCmd = init ()
    let model = ref initModel
    let modelEventHandler = new ModelEventHandler<'model>()
    let msgEventHandler = Event<'msg>()
    let sendMsg msg = msgEventHandler.Trigger msg
    let properties =
      try
        (!model).GetType().GetProperties()
      with _ -> Array.empty
    msgEventHandler.Publish.Add(fun msg ->
      let newModel, cmd = update !model msg
      if !model <> newModel then
        let newBinding = view.BindModel newModel
        match Array.isEmpty properties with
        | true ->
          UIContext.current.Post((fun _ -> QuotationEvaluator.Evaluate newBinding), null)
        | false -> diff properties !model newModel newBinding modelEventHandler.NotifyChange
        model := newModel
      Cmd.exec sendMsg cmd
    )
    view.BindModel !model |> QuotationEvaluator.Evaluate
    view.BindMsg sendMsg
    Cmd.exec sendMsg initCmd

  let run init update view = run' { Init = init; Update = update; View = view }
