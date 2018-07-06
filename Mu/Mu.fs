namespace Mu

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Reflection

type Update<'model, 'action> =
  | NoUpdate
  | Update of 'model
  | UpdateWithSideEffects of 'model * SideEffects<'model, 'action>
  | SideEffects of SideEffects<'model, 'action>
and SideEffects<'model, 'action> =
  'model -> Action<'action> -> unit
and Action<'action> =
  'action -> unit

type IView<'model, 'action> =
  abstract BindModel: 'model -> IBinder -> unit
  abstract BindAction: Action<'action> -> unit
and IBinder =
  abstract Bind: Expr<'value> -> ('value -> unit) -> unit

type private Binder () =
  let curSyncContext = System.Threading.SynchronizationContext.Current
  let event = Event<string * obj> ()

  member __.OnChange = event.Publish

  member __.NotifyChange field value =
    // Ensure event triggering in UI thread/context
    curSyncContext.Post ((fun _ -> event.Trigger (field, value)), null)

  interface IBinder with
    member x.Bind (getter: Expr<'a>) (updateView: 'a -> unit) =
      match getter with
      | PropertyGet (Some (Value(target, _)), propInfo, [])  ->
        updateView (propInfo.GetValue target :?> 'a)
        x.OnChange
        |> Observable.filter (fun (name, _) -> name = propInfo.Name)
        |> Observable.add (fun (_, value) -> updateView (value :?> 'a))
      | _ ->
        failwith "Expression must be a record field"

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

  let run' (t:T<'model, 'action>) =
    let { T.init = init; update = update; view = view } = t
    let model = init () |> ref
    let actionHolder = Event<'action> ()
    let action = actionHolder.Trigger
    let binder = Binder ()
    actionHolder.Publish.Add (fun e ->
      match update !model e with
      | NoUpdate ->
        () // Do nothing
      | Update newModel ->
        diff !model newModel binder.NotifyChange
        model := newModel
      | UpdateWithSideEffects (newModel, effects) ->
        diff !model newModel binder.NotifyChange
        model := newModel
        effects !model action
      | SideEffects effects ->
        effects !model action
    )
    view.BindModel !model binder
    view.BindAction action

  let run init update view =
    run' { init = init; update = update; view = view }
