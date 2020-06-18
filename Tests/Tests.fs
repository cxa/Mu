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
  | Incr i -> Update { Count = model.Count + i }
  | Decr i -> Update { Count = model.Count - i }

type View() =
  member val Count = 0 with get, set
  member val Send: Send<Msg> = (fun _ -> ()) with get, set

  interface IView<Model, Msg> with
    member x.BindModel model = <@ x.Count <- model.Count @>

    member x.BindMsg send = x.Send <- send

[<Fact>]
let ``View updates as expect``() =
  let initCount = Random().Next 100
  let init() = { Count = initCount }
  let view = View()
  Mu.run init update view
  Assert.Equal(view.Count, initCount)
  view.Send <| Decr 4
  Assert.Equal(view.Count, initCount - 4)
  view.Send <| Incr 10
  Assert.Equal(view.Count, initCount - 4 + 10)
