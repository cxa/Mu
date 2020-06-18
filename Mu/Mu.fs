namespace Mu

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Reflection
open System.Threading
open FSharp.Quotations.Evaluator

type Send<'msg> = 'msg -> unit

type Eff<'model, 'msg> = 'model -> Send<'msg> -> unit

type Update<'model, 'msg> =
  | NoUpdate
  | Update of 'model
  | UpdateWithEffects of 'model * Effects<'model, 'msg>
  | Effects of Effects<'model, 'msg>

and Effects<'model, 'msg> =
  | Eff of Eff<'model, 'msg>
  | Cmd of ('model -> 'msg) // Sync cmd
  | Cmd' of ('model -> Async<'msg>) // Async cmd
  | Cmd'' of ('model -> (Async<'msg> * CancellationTokenSource)) // Cancellable anync cmd

type IView<'model, 'msg> =
  abstract BindModel: 'model -> Expr<unit>
  abstract BindMsg: Send<'msg> -> unit


type private ModelEventHandler<'model>() =

  let uiSyncContext =
    let ctx = SynchronizationContext.Current
    if isNull ctx then
      failwith
        "Can't get UI SynchronizationContext, make sure you run Mu afer UI application initialized"
    ctx

  let rec splitExpr =
    function
    | Sequential(h, t) -> h :: splitExpr t
    | t -> [ t ]

  let rec exprContainsFields fields expr =
    let thatExpr = exprContainsFields fields
    match expr with
    | Application(e1, e2) -> thatExpr e1 || thatExpr e2
    | Call(eOpt, _methodInfo, eList) ->
        (eOpt
         |> Option.map thatExpr
         |> Option.defaultValue false)
        || (eList |> List.exists thatExpr)
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
    | PropertyGet(Some(Value(o, _)), propInfo, []) when (o :? 'model) ->
        Set.contains propInfo.Name fields
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
      |> List.iter (QuotationEvaluator.EvaluateUntyped >> ignore))
    evt

  member __.NotifyChange fields expr =
    uiSyncContext.Send((fun _ -> event.Trigger(fields, expr)), null)

module Mu =
  type T<'model, 'msg> =
    { Init: unit -> 'model
      Update: 'model -> 'msg -> Update<'model, 'msg>
      View: IView<'model, 'msg> }

  let private diff m1 m2 expr cb =
    let diffFields =
      FSharpType.GetRecordFields(m1.GetType())
      |> Seq.fold (fun acc field ->
           let v1, v2 = field.GetValue m1, field.GetValue m2
           if v1 <> v2 then Set.add field.Name acc else acc) Set.empty

    if Set.isEmpty diffFields then () else cb diffFields expr

  let private startAsync msgAsync (cancelSrc: CancellationTokenSource) sendMsg =
    let computation =
      async {
        let! msg = msgAsync
        sendMsg msg }

    if isNull cancelSrc
    then Async.Start computation
    else Async.Start (computation, cancelSrc.Token)

  let private handleEffects effects currentModel sendAction =
    match effects with
    | Eff fn -> fn currentModel sendAction
    | Cmd fn -> sendAction (fn currentModel)
    | Cmd' fn ->
        let msgAsync = fn currentModel
        startAsync msgAsync null sendAction
    | Cmd'' fn ->
        let msgAsync, cancelSource = fn currentModel
        startAsync msgAsync cancelSource sendAction

  // should always run in UI thread
  let run' t =
    let { T.Init = init; Update = update; View = view } = t
    let model = init() |> ref
    let modelEventHandler = ModelEventHandler()
    let msgEventHandler = Event<'msg>()
    let sendMsg = msgEventHandler.Trigger
    msgEventHandler.Publish.Add(fun msg ->
      match update !model msg with
      | NoUpdate -> ()
      | Update newModel ->
          diff !model newModel (view.BindModel newModel) modelEventHandler.NotifyChange
          model := newModel
      | UpdateWithEffects(newModel, effects) ->
          diff !model newModel (view.BindModel newModel) modelEventHandler.NotifyChange
          model := newModel
          handleEffects effects !model sendMsg
      | Effects effects -> handleEffects effects !model sendMsg)
    view.BindModel !model |> QuotationEvaluator.Evaluate
    view.BindMsg sendMsg

  let run init update view =
    run'
      { Init = init
        Update = update
        View = view }
