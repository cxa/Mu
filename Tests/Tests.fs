module Tests

open System
open Xunit
open Mu

type Model = { Count: int }

type Action =
  | Incr of int
  | Decr of int

let update model event =
  match event with
  | Incr i -> Update { Count = model.Count + i }
  | Decr i -> Update { Count = model.Count - i }

type View () =
  member val Count = 0 with get, set
  member val Send: Action<Action> = (fun _ -> ()) with get, set

  interface IView<Model, Action> with
    member x.BindModel model =
      <@ x.Count <- model.Count @>

    member x.BindAction send =
      x.Send <- send

[<Fact>]
let ``Test View Update`` () =
  let initCount = Random().Next 100
  let init () = { Count = initCount }
  let view = View ()
  Mu.run init update view
  Assert.Equal (view.Count, initCount)
  view.Send <| Decr 4
  Async.Sleep 1000 |> Async.RunSynchronously
  Assert.Equal (view.Count, initCount - 4)
  view.Send <| Incr 10
  Async.Sleep 1000 |> Async.RunSynchronously
  Assert.Equal (view.Count, initCount - 4 + 10)
