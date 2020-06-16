namespace Mu

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Reflection
open System.Threading
open FSharp.Quotations.Evaluator

type Update<'model, 'action> =
  | NoUpdate
  | Update of 'model
  | UpdateWithEffects of 'model * Effects<'model, 'action>
  | Effects of Effects<'model, 'action>

and Effects<'model, 'action> =
  | Eff of ('model -> unit)
  | Cmd of ('model -> 'action)
  | AsyncCmd of ('model -> Async<'action>)
  | AsyncCmd' of ('model -> (Async<'action> * CancellationTokenSource))

type IView<'model, 'action> =
  abstract BindModel: 'model -> Expr<unit>
  abstract BindAction: Action<'action> -> unit

and Action<'action> = 'action -> unit

type private ModelChangeMonitor<'model>() =
  let uiSyncContext = 
    let ctx = SynchronizationContext.Current
    if isNull ctx then failwith "Can't get UI SynchronizationContext, make sure you run Mu afer UI application initialized"
    ctx

  let rec splitExpr =
    function
    | Sequential (h, t) -> h :: splitExpr t
    | t -> [ t ]

  let rec isExprDependedOnModelField fields expr =
    let thatExpr = isExprDependedOnModelField fields
    match expr with
    | Application (e1, e2) -> thatExpr e1 || thatExpr e2
    | Call (eOpt, _methodInfo, eList) ->
      (eOpt
       |> Option.map thatExpr
       |> Option.defaultValue false)
      || (eList |> List.exists thatExpr)
    | Coerce (e, _) -> thatExpr e
    | FieldGet (Some (e), _) -> thatExpr e
    | FieldSet (Some (e), _, e2) -> thatExpr e || thatExpr e2
    | ForIntegerRangeLoop (_, e, e2, e3) -> thatExpr e || thatExpr e2 || thatExpr e3
    | IfThenElse (e, e2, e3) -> thatExpr e || thatExpr e2 || thatExpr e3
    | Lambda (_, e) -> thatExpr e
    | Let (_, e1, e2) -> thatExpr e1 || thatExpr e2
    | LetRecursive (el, e2) ->
      thatExpr e2
      || el
      |> List.exists (fun (_, e) -> thatExpr e)
    | NewArray (_, el) -> el |> List.exists thatExpr
    | NewDelegate (_, _, e) -> thatExpr e
    | NewObject (_, el) -> el |> List.exists thatExpr
    | NewRecord (_, el) -> el |> List.exists thatExpr
    | NewTuple (el) -> el |> List.exists thatExpr
    | NewUnionCase (_, el) -> el |> List.exists thatExpr
    | PropertySet (_, _, _, e) -> thatExpr e
    | PropertyGet (Some (Value (o, _)), propInfo, []) when (o :? 'model) -> Set.contains propInfo.Name fields
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
    evt.Publish.Add (fun (changedFields, expr) ->
      expr
      |> splitExpr
      |> List.filter (isExprDependedOnModelField changedFields)
      |> List.iter (QuotationEvaluator.EvaluateUntyped >> ignore))
    evt

  member __.NotifyChange field expr =
    uiSyncContext.Send((fun _ -> event.Trigger(field, expr)), null)

module Mu =
  type T<'model, 'action> =
    { Init: unit -> 'model
      Update: 'model -> 'action -> Update<'model, 'action>
      View: IView<'model, 'action> }

  let private diff m1 m2 expr cb =
    let diffFields =
      FSharpType.GetRecordFields(m1.GetType())
      |> Seq.fold (fun acc field ->
        let v1, v2 = field.GetValue m1, field.GetValue m2
        if v1 <> v2 then Set.add field.Name acc else acc) Set.empty

    if Set.isEmpty diffFields then () else cb diffFields expr

  let private startAsync actionAsync (cancelSrc: CancellationTokenSource) sendAction =
    let computation =
      async {
        let! action = actionAsync
        sendAction action
      }

    if not (isNull cancelSrc)
    then Async.StartImmediate(computation, cancelSrc.Token)

  let private handleEffects effects currentModel sendAction =
    match effects with
    | Eff fn -> fn currentModel
    | Cmd fn -> sendAction (fn currentModel)
    | AsyncCmd fn ->
      let actionAsync = fn currentModel
      startAsync actionAsync null sendAction
    | AsyncCmd' fn ->
      let actionAsync, cancelSource = fn currentModel
      startAsync actionAsync cancelSource sendAction

  let run' t =
    let { T.Init = init; Update = update; View = view } = t
    let model = init () |> ref
    let actionMonitor = Event<'action>()
    let sendAction = actionMonitor.Trigger
    let modelMonitor = ModelChangeMonitor()
    actionMonitor.Publish.Add(fun e ->
      match update !model e with
      | NoUpdate -> ()
      | Update newModel ->
        diff !model newModel (view.BindModel newModel) modelMonitor.NotifyChange
        model := newModel
      | UpdateWithEffects (newModel, effects) ->
        diff !model newModel (view.BindModel newModel) modelMonitor.NotifyChange
        model := newModel
        handleEffects effects !model sendAction
      | Effects effects -> handleEffects effects !model sendAction)

    view.BindModel !model
    |> QuotationEvaluator.EvaluateUntyped
    |> ignore
    view.BindAction sendAction

  // should always run in UI thread
  let run init update view =
    run' { Init = init; Update = update; View = view }
