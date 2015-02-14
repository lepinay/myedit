#r @"PresentationCore"
#r @"PresentationFramework"
#r @"WindowsBase"
#r @"System.Xaml"
#r @"UIAutomationTypes"
#r @"packages\Rx-Core.2.2.5\lib\net45\System.Reactive.Core.dll"
#r @"packages\Rx-Interfaces.2.2.5\lib\net45\System.Reactive.Interfaces.dll"
#r @"packages\Rx-Linq.2.2.5\lib\net45\System.Reactive.Linq.dll"
#r @"packages\FSharpx.TypeProviders.Xaml.1.8.41\lib\40\FSharpx.TypeProviders.Xaml.dll"
#r @"packages\AvalonEdit.5.0.2\lib\Net40\ICSharpCode.AvalonEdit.dll"
#r @"packages\Simple.Wpf.Terminal.1.33.0.0\lib\net40\Simple.Wpf.Terminal.dll"
#r @"packages\Rx-PlatformServices.2.2.5\lib\net45\System.Reactive.PlatformServices.dll"
#r @"packages\reactiveui-core.6.4.0.1\lib\Net45\ReactiveUI.dll"
#r @"packages\Splat.1.6.0\lib\Net45\Splat.dll"

open System
open System.Windows          
open System.Windows.Controls  
open System.Windows.Media
open System.Windows.Shapes
open System.Reactive
open System.Reactive.Linq
open FSharpx
open System.Windows.Data
open ICSharpCode.AvalonEdit
open Simple.Wpf.Terminal
open System.Xml
open ICSharpCode.AvalonEdit.Highlighting
open ICSharpCode.AvalonEdit.Document

//open ReactiveUI

//#region helpers
type Column = GridLength
type Row = GridLength
type Title = String
type SplitterDirection = Horizontal | Vertical


type Command =
    | BrowseFile
    | BrowseFolder
    | SaveFile of string
    | OpenFile of string

type Element = 
    | Docked of Element*Dock
    | Column of Element*int
    | Row of Element*int
    | Dock of Element list
    | Menu of Element list
    | MenuItem of Title*Element list*Command list
    | Grid of Column list*Row list*Element list
    | Splitter of SplitterDirection
    | Terminal
    | Tree of Element list
    | TreeItem of Title*Element list
    | Editor of TextDocument*int
    | Tab of (String*Element*Boolean) list


type EditorState = {
    openFiles:(string*TextDocument*int*bool) list
}


let messages = new Reactive.Subjects.Subject<Command>()

let openFile () = 
    let dlg = new Microsoft.Win32.OpenFileDialog();
    dlg.DefaultExt <- ".cs";
    dlg.Filter <- "CSharp Files (*.cs)|*.cs|Haskell Files (*.hs)|*.hs|FSharp Files (*.fs)|*.fs";
    let result = dlg.ShowDialog();
    if result.HasValue && result.Value then
        messages.OnNext(OpenFile dlg.FileName)
    ()

let openFolder () = 
    printfn "open a folder"
    ()

let saveFile () = 
    printfn "save a file"
    ()

let ui (state:EditorState) = 
    let tabs = state.openFiles |> List.map (fun (t,p,pos,selected) -> (t,Editor (p,pos),selected ) )
    Dock [Docked(Menu [MenuItem ("File",
                        [
                        MenuItem ("Open file",[], [BrowseFile] )
                        MenuItem ("Open folder",[], [BrowseFolder])
                        MenuItem ("Save",[], [SaveFile ""])], [])],Dock.Top)
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

let haskellSyntax = 
    use reader = new XmlTextReader(System.IO.Path.Combine( __SOURCE_DIRECTORY__ ,@"Syntax\FS-Mode.xshd"))
    ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);

let reuse (prev:obj) fact = 
    if prev <> null && prev :? 'a then 
        printfn "reusing %A" prev
        downcast prev
    else 
        printfn "not reusing %A because need %A" prev typeof<'a>
        fact()

let reuseChildrens d (childrens:UIElementCollection) xs render =
    if childrens.Count = List.length xs then 
        let _childrens = seq {for c in childrens -> c} |> Seq.toList
        for (x,child) in (Seq.zip xs _childrens) do 
            let rendered = render child x
            if(rendered <> child) then 
                let pos = childrens.IndexOf(child)
                childrens.Remove(child)
                childrens.Insert(pos,rendered) |> ignore
                printfn "%A Child %A NOT reused" d child
            else printfn "%A Child %A reused" d child
    else 
        childrens.Clear()
        for x in xs do childrens.Add (render null x) |> ignore

let rec render (prev:obj) ui : UIElement = 
    match ui with
        | Dock xs -> 
            let d = reuse prev (fun () -> new DockPanel(LastChildFill=true))
            reuseChildrens d d.Children xs render
            d :> UIElement
        | Terminal -> new Terminal() :> UIElement
        | Tab xs ->
            let t = new TabControl()
            for (title,e,selected) in xs do
                let ti = new TabItem()
                ti.Content <- render null e
                ti.Header <- title
                ti.IsSelected <- selected
                t.Items.Add(ti) |> ignore
            t :> UIElement
        | Menu xs ->
            let m = reuse prev (fun () -> new Menu())
            for x in xs do m.Items.Add (render null x) |> ignore
            m :> UIElement
        | MenuItem (title,xs,actions) -> 
            let mi = Controls.MenuItem(Header=title) 
            for x in xs do mi.Items.Add(render null x) |> ignore
            match actions with 
                | [BrowseFile] -> mi.Click |> Observable.subscribe(fun e -> openFile()) |> ignore
                | other -> ()
            mi :> UIElement
        | Grid (cols,rows,xs) ->
            let g = reuse prev (fun () -> new Grid())

            // reuse this if possible
            g.RowDefinitions.Clear()
            g.ColumnDefinitions.Clear()
            for row in rows do g.RowDefinitions.Add(new RowDefinition(Height=row))
            for col in cols do g.ColumnDefinitions.Add(new ColumnDefinition(Width=col))

            reuseChildrens g g.Children xs render
            g :> UIElement
        | Docked (e,d) ->
            let elt = render prev e
            DockPanel.SetDock(elt,d)
            elt
        | Column (e,d) ->
            let elt = render prev e
            Grid.SetColumn(elt,d)
            elt
        | Row (e,d) ->
            let elt = render prev e
            Grid.SetRow(elt,d)
            elt
        | Splitter Vertical ->
            // check the gs has the same definition
            let gs = reuse prev <| fun () -> new GridSplitter(VerticalAlignment=VerticalAlignment.Stretch,HorizontalAlignment=HorizontalAlignment.Center,ResizeDirection=GridResizeDirection.Columns,ShowsPreview=true,Width=5.)
            gs :> UIElement
        | Splitter Horizontal ->
            let gs = reuse prev <| fun () -> new GridSplitter(VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Stretch,ResizeDirection=GridResizeDirection.Rows,ShowsPreview=true,Height=5.)
            gs :> UIElement
        | Tree xs ->
            let t = new TreeView()
            for x in xs do t.Items.Add(render null x) |> ignore
            t :> UIElement
        | TreeItem (title,xs) ->
            let ti = new TreeViewItem()
            ti.Header <- title
            for x in xs do ti.Items.Add(render null x) |> ignore
            ti :> UIElement
        | Editor (doc,pos) ->
            let editor = new TextEditor()
            editor.CaretOffset <- pos
            editor.Document <- doc
            editor.SyntaxHighlighting <- haskellSyntax
            editor :> UIElement
        | other -> failwith "not handled"
    

let w =  new Window(Title="F# is fun!",Width=260., Height=420., Topmost = true)

w.Show()
w.Content <- render null <| ui {openFiles=[]}        


messages.Scan({openFiles=[]},
            fun state cmd ->
                match cmd with 
                | OpenFile s ->
                    let unselect = state.openFiles |> List.map( fun (a,b,c,_) -> (a,b,c,false) ) 
                    let content = IO.File.ReadAllText(s)
                    let doc = new ICSharpCode.AvalonEdit.Document.TextDocument(content)
                    {state with openFiles=("file 1",doc,0,true)::unselect }      
                | other -> state  )
        .Scan((ui {openFiles=[]},ui {openFiles=[]}), fun (prevdom,newdom) state -> (newdom,ui state) )
        .Subscribe(function (p,c) -> w.Content <- render w.Content c  )


//        | OpenFile s -> w.Content <- render <| ui {openFiles=[("file 1",s)]}        










