namespace Counter.Core

module MainComponent =
  open Mu
  open System.Threading

  type Model =
    { Number: int
      Requesting: bool
      RequestingTokenSource: CancellationTokenSource option
      ButtonTitle: string
      Message: string }

  module Action =
    type T =
      | Incr
      | Decr
      | HandleRandomizing
      | RequestRandomYear
      | UserCancel
      | ReceiveRandomYear of Result<string, exn>

  module private Effects =
    let requestRandom tkSource _model =
      async {
        use wc = new System.Net.WebClient ()
        let! choice =
            wc.DownloadStringTaskAsync "http://numbersapi.com/random/year"
            |> Async.AwaitTask
            |> Async.Catch
        let result =
          match choice with
          | Choice1Of2 str -> Ok str
          | Choice2Of2 err -> Error err
        return (Action.ReceiveRandomYear result)
      }, tkSource

  let init () =
    { Number = System.Random().Next 100
      Requesting = false
      RequestingTokenSource = None
      ButtonTitle = "Randomize"
      Message = "" }

  let update model action =
    match action with
    | Action.Incr -> Update { model with Number = model.Number + 1 }
    | Action.Decr -> Update { model with Number = model.Number - 1 }
    | Action.HandleRandomizing ->
      let cmd = Cmd (fun model ->
          if model.Requesting
          then Action.UserCancel
          else Action.RequestRandomYear)
      Effects cmd
    | Action.RequestRandomYear ->
      let ts = new CancellationTokenSource ()
      UpdateWithEffects (
        { model with
            Requesting = true
            RequestingTokenSource = Some ts
            ButtonTitle = "Cancel"
            Message = "Requesting a random year..." },
        AsyncCmd' (Effects.requestRandom ts))
    | Action.UserCancel ->
      UpdateWithEffects (
        { model with
            Requesting = false
            ButtonTitle = "Randomize"
            Message = "" },
        Eff (fun model ->
          model.RequestingTokenSource
          |> Option.iter (fun s -> s.Cancel ())))
    | Action.ReceiveRandomYear (Ok str) ->
      let number = str.Split (' ') |> Array.head |> int // unsafe here
      Update
        { model with
            RequestingTokenSource = None
            Requesting = false
            ButtonTitle = "Randomize"
            Number = number
            Message = str }
    | Action.ReceiveRandomYear (Error err) ->
      Update
        { model with
            RequestingTokenSource = None
            Requesting = false
            ButtonTitle = "Randomize"
            Message = err.ToString () }

  let runInView view =
    Mu.run init update view
