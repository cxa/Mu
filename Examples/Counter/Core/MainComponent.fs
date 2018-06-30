namespace Counter.Core

module MainComponent =
  open Mu

  type Model =
    { Number: int
      Message: string }

  module Event =
    type T =
      | Incr
      | Decr
      | RequestRandom
      | RandomSucc of string
      | RandomErr of string

  module private Effects =
    let requestRandom model emit =
      let wc = new System.Net.WebClient ()
      async {
        let choice =
          wc.DownloadStringTaskAsync "http://numbersapi.com/random/year"
          |> Async.AwaitTask
          |> Async.Catch
          |> Async.RunSynchronously
        match choice with
        | Choice1Of2 str -> emit <| Event.RandomSucc str
        | Choice2Of2 err -> emit <| Event.RandomErr (err.ToString())
      } |> Async.Start

  let init () =
    { Number = System.Random().Next 100
      Message = "" }

  let update model event =
    match event with
    | Event.Incr -> Update { model with Number = model.Number + 1 }
    | Event.Decr -> Update { model with Number = model.Number - 1 }
    | Event.RequestRandom ->
      UpdateWithSideEffects 
        ({ model with Message = "Randomizing..." }, Effects.requestRandom)
    | Event.RandomSucc str ->
      let number = str.Split (' ') |> Array.head |> int // unsafe here
      Update { Number = number; Message = str }
    | Event.RandomErr err -> Update { model with Message = err }

  let runInView view =
    Mu.run init update view
