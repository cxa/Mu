# Mu

Apply The Elm Architecture pattern on Xamarin stack, with another simple, unobtrusive way.

[![Build Status](https://travis-ci.org/cxa/Mu.svg?branch=master)](https://travis-ci.org/cxa/Mu)

## The Elm Architecture (TEA)

[TEA](https://guide.elm-lang.org/architecture/) is all about Model, Update, and View, in **Mu**, it's specific to:

- Model — the state of a GUI component
- Update — a way to update your state
- View — a way to view your state as `UIView(Controller)`, `NSView(Controller)`, `Activity` and other platform view stacks that Xamarin supported

This is a simple pattern for architecting GUI components.

```fsharp
// Model
type Model = { ... }

// Msgs, all update through sending message to perform
type Msg = Reset | ...

// Update
let update model msg =
  match msg with
  | Reset -> Update { model with ... }
  ...

// View
type ViewController (handle:IntPtr) =
  inherit UIViewController (handle)
  ...

  override x.ViewDidLoad () =
    base.ViewDidLoad ()
    // Glue model update, and view
    Mu.run init update x

  interface Mu.IView<Model, Msg> with
    // Setup view and model relationship
    member x.BindModel model =
      <@
        x.someLabel.Text <- model.Field
        ...
      @>

    // Setup communicating between view and model, via aciton
    member x.BindMsg send =
      x.resetButton.TouchUpInside.Add (fun _ -> send Reset)
      ...
```

That is really the essence of The Elm Architecture!

With **Mu**, Model and Update are separated from view, this means that they can shared across platforms, implementing `Mu.IView<'model, 'acition>` interfaces is the only required step to support specific platform.

### Model

Model should be a DTO(Data Transfer Object) only, immutable record is the best way to represent model in F#.

### Update

```fsharp
type Send<'msg> = 'msg -> unit

type Eff<'model, 'msg> = 'model -> Send<'msg> -> unit

type Update<'model, 'msg> =
  | NoUpdate
  | Update of 'model
  | UpdateWithEffects of 'model * Effects<'model, 'msg>
  | Effects of Effects<'model, 'msg>

and Effects<'model, 'msg> =
  | Eff of Eff<'model, 'msg>
  | Cmd of ('model -> 'msg) // Sync cmd
  | Cmd' of ('model -> Async<'msg>) // Async cmd
  | Cmd'' of ('model -> (Async<'msg> * CancellationTokenSource)) // Cancellable anync cmd
```

Update is about changing model through msg: `'model -> 'msg -> Update<'model, 'msg>`.

Not all updates are just model changes, side effects without or with model changing are common. **Mu** provides current model and the message sender when performing side effects.

### View

```fsharp
type IView<'model, 'msg> =
  abstract BindModel: 'model -> Expr<unit>
  abstract BindMsg: Send<'msg> -> unit
```

View is only an interface in **Mu**, this is the most unobtrusive way to introduce 3rd lib into your project. Setup binding in `BindModel` to sync model states to view elements, and `BindMsg` provides a message sender to make obtaining user input possible.

### Run

A **Mu** component is simply a record contained model initialization, model updater and view:

```fsharp
type T<'model, 'msg> =
    { Init: unit -> 'model
      Update: 'model -> 'msg -> Update<'model, 'msg>
      View: IView<'model, 'msg> }
```

Run component on view when it's ready with:

```fsharp
Mu.run' component
// or
Mu.run init update view
```

## Examples

👉 [Examples](Examples) contain some advance usages of async effects.

## Usage

- Install from NuGet: [https://www.nuget.org/packages/com.realazy.Mu](https://www.nuget.org/packages/com.realazy.Mu)
- Add this project to your solution, or directly add `Mu.fs`. (Yes `Mu` is a single file project, less than 150 SLOC! 🤯)

## LICENSE

MIT

## If It Matters

Code formated with `fantomas --indent 2 --pageWidth 96`.

## Author

- Blog: [realazy.com](https://realazy.com) (Chinese)
- Github: [@cxa](https://github.com/cxa)
- Twitter: [@\_cxa](https://twitter.com/_cxa) (Chinese mainly)
