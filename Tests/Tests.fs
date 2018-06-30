module Tests

open System
open Xunit
open Mu

type Model = { Count: int }

type Event =
  | Incr of int
  | Decr of int

let update model event =
  match event with
  | Incr i -> Update { Count = model.Count + i }
  | Decr i -> Update { Count = model.Count - i }

type View () =
  member val Count = 0 with get, set
  member val Emit: EmitEvent<Event> = (fun _ -> ()) with get, set

  interface IView<Model, Event> with
    member x.BindModel model binder =
      binder.Bind <@ model.Count @> (fun c -> x.Count <- c)

    member x.BindEvent emit =
      x.Emit <- emit

[<Fact>]
let ``Test View Update`` () =
  let initCount = Random().Next 100
  let init () = { Count = initCount }
  let view = View ()
  Mu.run init update view
  Assert.Equal (view.Count, initCount)
  view.Emit <| Decr 4
  Async.Sleep 1000 |> Async.RunSynchronously
  Assert.Equal (view.Count, initCount - 4)
  view.Emit <| Incr 10
  Async.Sleep 1000 |> Async.RunSynchronously
  Assert.Equal (view.Count, initCount - 4 + 10)
