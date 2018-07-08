namespace Mu

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Reflection
open System.Threading

type Update<'model, 'action> =
  | NoUpdate
  | Update of 'model
  | UpdateWithEffects of 'model * Effects<'model, 'action>
  | Effects of Effects<'model, 'action>
and Effects<'model, 'action> =
  | Eff of ('model -> unit)
  | Cmd of ('model -> 'action)
  | AsyncCmd of ('model -> Async<'action>)
  | AsyncCmd' of ('model -> Async<'action> * CancellationTokenSource)

type IView<'model, 'action> =
  abstract BindModel: 'model -> IBinder<'action> -> unit
  abstract BindAction: Action<'action> -> unit
and IBinder<'action> =
  abstract Bind: Expr<'value> -> ('value -> unit) -> unit
  abstract Send: Action<'action>
and Action<'action> =
  'action -> unit

type private Binder<'action> (send: Action<'action>) =
  let curSyncContext = System.Threading.SynchronizationContext.Current
  let event = Event<string * obj> ()

  member __.OnChange = event.Publish

  member __.NotifyChange field value =
    // Ensure event triggering in UI thread/context
    curSyncContext.Post ((fun _ -> event.Trigger (field, value)), null)

  interface IBinder<'action> with
    member x.Bind (getter: Expr<'a>) (updateView: 'a -> unit) =
      match getter with
      | PropertyGet (Some (Value(target, _)), propInfo, [])  ->
        updateView (propInfo.GetValue target :?> 'a)
        x.OnChange
        |> Observable.filter (fun (name, _) -> name = propInfo.Name)
        |> Observable.add (fun (_, value) -> updateView (value :?> 'a))
      | _ ->
        failwith "Expression must be a record field"

    member __.Send = send

module Mu =
  type T<'model, 'action> =
    { init: unit -> 'model
      update: 'model -> 'action -> Update<'model, 'action>
      view: IView<'model, 'action> }

  let private diff m1 m2 cb =
    FSharpType.GetRecordFields (m1.GetType ())
    |> Seq.iter (fun f ->
      let v1, v2 = f.GetValue m1, f.GetValue m2
      if v1 <> v2 then cb f.Name v2
    )

  let private startAsync actionAsync (cancelSrc:CancellationTokenSource) sendAction =
    let computation =
      async { let! action = actionAsync in sendAction action }
    if not (isNull cancelSrc) then
      Async.StartImmediate (computation, cancelSrc.Token)

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

  let run' (t:T<'model, 'action>) =
    let { T.init = init; update = update; view = view } = t
    let model = init () |> ref
    let actionHolder = Event<'action> ()
    let sendAction = actionHolder.Trigger
    let binder = Binder (sendAction)
    actionHolder.Publish.Add (fun e ->
      match update !model e with
      | NoUpdate -> ()
      | Update newModel ->
        diff !model newModel binder.NotifyChange
        model := newModel
      | UpdateWithEffects (newModel, effects) ->
        diff !model newModel binder.NotifyChange
        model := newModel
        handleEffects effects !model sendAction
      | Update.Effects effects ->
        handleEffects effects !model sendAction)
    view.BindModel !model binder
    view.BindAction sendAction

  let run init update view =
    run' { init = init; update = update; view = view }
