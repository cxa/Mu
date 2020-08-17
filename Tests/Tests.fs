module Tests

open System
open Xunit
open Mu

type Model =
  { Count: int }

type Msg =
  | Incr of int
  | Decr of int

let update model msg =
  match msg with
  | Incr i ->
    { model with Count = model.Count + i }, Cmd.none

  | Decr i ->
    { model with Count = model.Count - i }, Cmd.none

type View() =
  member val Count = 0 with get, set
  member val Send: Send<Msg> = (fun _ -> ()) with get, set

  interface IView<Model, Msg> with
    member x.BindModel model =
      <@ x.Count <- model.Count @>

    member x.BindMsg send =
      x.Send <- send

[<Fact>]
let ``View updates as expect``() =
  let initCount = Random().Next 100
  let init() = { Count = initCount }
  let view = View()
  Mu.run init update view
  // UI updates are async, we use an `Async.Sleep` for delay checking
  async {
    do! Async.Sleep(10)
    Assert.Equal(view.Count, initCount)
    view.Send <| Decr 4
    do! Async.Sleep(10)
    Assert.Equal(view.Count, initCount - 4)
    view.Send <| Incr 10
    do! Async.Sleep(10)
    Assert.Equal(view.Count, initCount - 4 + 10)
  } |> Async.StartImmediate
