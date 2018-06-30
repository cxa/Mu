namespace Mu

module Binder =
  open Microsoft.FSharp.Quotations
  open Microsoft.FSharp.Quotations.Patterns

  type I =
    abstract Bind: Expr<'a> -> ('a -> unit) -> unit

  type internal T () =
    let curSyncContext = System.Threading.SynchronizationContext.Current
    let event = Event<string * obj> ()

    member __.OnChange = event.Publish

    member __.Emit field value =
      // Ensure event triggering in UI thread/context
      curSyncContext.Post ((fun _ -> event.Trigger (field, value)), null)

    interface I with
      member x.Bind (getter: Expr<'a>) (updateView: 'a -> unit) =
        match getter with
        | PropertyGet (Some (Value(target, _)), propInfo, [])  ->
          updateView (propInfo.GetValue target :?> 'a)
          x.OnChange
          |> Observable.filter (fun (name, _) -> name = propInfo.Name)
          |> Observable.add (fun (_, value) -> updateView (value :?> 'a))
        | _ ->
          failwith "Expression must be a property getter"

type EmitEvent<'event> =
  'event -> unit

type SideEffects<'model, 'event> =
  'model -> EmitEvent<'event> -> unit

type Update<'model, 'event> =
  | Update of 'model
  | UpdateWithSideEffects of 'model * SideEffects<'model, 'event>
  | SideEffects of SideEffects<'model, 'event>

type IView<'model, 'event> =
  abstract BindModel: 'model -> Binder.I -> unit
  abstract BindEvent: EmitEvent<'event> -> unit

type T<'model, 'event> =
  { init: unit -> 'model
  ; update: 'model -> 'event -> Update<'model, 'event>
  ; view: IView<'model, 'event>
  }

module Mu =
  open Microsoft.FSharp.Reflection

  let private diff m1 m2 cb =
    let fields = FSharpType.GetRecordFields (m1.GetType ())
    fields
    |> Seq.iter (fun f ->
      let v1 = f.GetValue m1
      let v2 = f.GetValue m2
      if v1 <> v2 then
        cb f.Name v2
   )

  let run' (t:T<'model, 'event>) =
    let { T.init = init
          update = update
          view = view
        } = t
    let model = init () |> ref
    let event = Event<'event> ()
    let emit = event.Trigger
    let binder = Binder.T ()
    event.Publish.Add (fun e ->
      match update !model e with
      | Update newModel ->
        diff !model newModel binder.Emit
        model := newModel
      | UpdateWithSideEffects (newModel, effects) ->
        diff !model newModel binder.Emit
        model := newModel
        effects !model emit
      | SideEffects effects ->
        effects !model emit
    )
    view.BindModel !model binder
    view.BindEvent emit

  let run init update view =
    run' { init = init; update = update; view = view }
