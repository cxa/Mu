namespace Counter.Core

module MainComponent =
  open Mu

  type Model =
    { Number: int
      Message: string }

  module Action =
    type T =
      | Incr
      | Decr
      | RequestRandom
      | RandomSucc of string
      | RandomErr of string

  module private Effects =
    let requestRandom model send =
      let wc = new System.Net.WebClient ()
      async {
        let choice =
          wc.DownloadStringTaskAsync "http://numbersapi.com/random/year"
          |> Async.AwaitTask
          |> Async.Catch
          |> Async.RunSynchronously
        match choice with
        | Choice1Of2 str -> send <| Action.RandomSucc str
        | Choice2Of2 err -> send <| Action.RandomErr (err.ToString())
      } |> Async.Start

  let init () =
    { Number = System.Random().Next 100
      Message = "" }

  let update model action =
    match action with
    | Action.Incr -> Update { model with Number = model.Number + 1 }
    | Action.Decr -> Update { model with Number = model.Number - 1 }
    | Action.RequestRandom ->
      UpdateWithSideEffects
        ({ model with Message = "Randomizing..." }, Effects.requestRandom)
    | Action.RandomSucc str ->
      let number = str.Split (' ') |> Array.head |> int // unsafe here
      Update { Number = number; Message = str }
    | Action.RandomErr err -> Update { model with Message = err }

  let runInView view =
    Mu.run init update view
