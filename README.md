# myedit

This is a **proof of concept** to see how hard it would be to write a code editor that

* Is native (WPF), no html/javascript: I really like the idea behind Brackets/Atom but here I want things blazing fast
* Embeded terminal to be able to run command within the editor
* Embeded file watcher that could trigger custom command line scripts
* Multi-rows of tabs, I love tabs, I open a lot of them, I don't like to scroll tabs !

#Why
Why am I writing my own code editor ?

* It's a fun exercise
* I like to learn different programming lanugages but I have not yet found lightweight windows code editor
* Lightweight to me mean: fast to start, reactive UI, bare minimun features
* I want multi row tabs
* I want an embeded command shell
* I want to be able to trigger script on file change and set that up EASILY

# History

This is where we have a fun and see how the baby is growing :smiley:

## Version 4

Mainly a lot of refactoring, I've replaced most of the c# code to f# code.
**All the meat is now in ui.fsx**
I'll remove soon all legacy c# code once I'll have migrated the terminal.

The architecture is now FRP:

- a state for the whole application
- a function that handle events using a fold on the state
- a renderer that turn the state into a virtual DOM representation of the app
- a solver that will diff the previous DOM and the current DOM into WPF code

This is inspired by [Elm](http://elm-lang.org/) and [React.js](http://facebook.github.io/react/)
FRP is implemented thanks to [Reactive extensions](https://github.com/Reactive-Extensions)

I'm really happy to be able to run away from event handlers, data binding and XAML !

This is how the view code looks like now:
```F#
let ui (state:EditorState) = 
    let tabs = state.openFiles |> List.map (fun (t,p,pos,selected) -> TabItem (t,Editor (p,pos),selected ) )
    Dock [Docked(Menu [MenuItem ("File",
                        [
                        MenuItem ("Open file",[], [BrowseFile] )
                        MenuItem ("Open folder",[], [BrowseFolder])
                        MenuItem ("Save",[], [SaveFile])], [])],Dock.Top)
          Grid ([GridLength(1.,GridUnitType.Star);GridLength(5.);GridLength(2.,GridUnitType.Star)],[],[
                    Column(Tree [TreeItem("Code",[TreeItem("HelloWorld",[])])] ,0)
                    Column(Splitter Vertical,1)
                    Column(
                        Grid ([GridLength(1.,GridUnitType.Star)],[GridLength(1.,GridUnitType.Star);GridLength(5.);GridLength(1.,GridUnitType.Star)],
                            [
                                Row(Tab tabs,0)
                                Row(Splitter Horizontal,1)
                                Row(Terminal,2)
                            ]),2)
                ])
    ]
```

Compare this to the hundred of xaml code !

## Version 3

* Added ability to save file
* We now display an '*' when the file is modified and not saved
* Added a toolbar (stole that from avalon edit sample)

###My feeling
Codewise things are really starting to get out of hand. The more component I add the more it's starting to get difficult to
find my way in all the event handling and data binding.

It's funny to see how fast you can get started and how fast thing get quickly ugly :D

I'll now try to spend more time refactoring the code and see if I can introduce some FRP for my own sanity.
I want to be able to clearly see:
* My data model
* The events
* How the ui is built from that

Some resource on WPF and FRP
http://steellworks.blogspot.com/2014/03/tutorial-functional-reactive.html


![shot](/Versions/3.png) 

## Version 2

Now I can open a folder and expand sub folders. Clicking on a file opens it the currently selected tab.

![shot](/Versions/2.png) 

## Version 1

### Design 

* Really poor but we start to get a feel of multirows tabs
* Tree File browser control in place but not coded up
* File menu in place but not wired up

### Code editor
* This is avalon edit, it just works
  
### Terminal
* It's possible to send command and it displays the output !
* It's slow !

![shot](/Versions/1.png) 






