module main

open System
open FsXaml

open System
open System.Windows          
open System.Windows.Controls  
open System.Reactive.Linq
open ICSharpCode.AvalonEdit
open Simple.Wpf.Terminal
open System.Xml
open ICSharpCode.AvalonEdit.Highlighting
open ICSharpCode.AvalonEdit.Document
open System.Windows.Media
open MyEdit.Powershell
open System.Management.Automation.Runspaces
open System.Management.Automation
open System.Threading.Tasks
open System.Reactive.Concurrency


//#region helpers
type Column = GridLength
type Row = GridLength
type Title = String
type SplitterDirection = Horizontal | Vertical


type Command =
    | BrowseFile
    | BrowseFolder
    | SaveFile
    | OpenFile of string
    | TextChanged of TextDocument
    | CommandOutput of string

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
    | TabItem of (String*Element*Boolean)
    | TextArea of string
    | Tab of Element list
    | Scroll of Element


type EditorState = {
    openFiles:(string*TextDocument*int*bool) list
    watches : (string) list
    consoleOutput : string
}


let debug = System.Diagnostics.Debug.WriteLine
let messages = new Reactive.Subjects.Subject<Command>()

let openFile () = 
    let dlg = new Microsoft.Win32.OpenFileDialog();
    dlg.DefaultExt <- ".cs";
    dlg.Filter <- "All Files (*.*)|*.*|CSharp Files (*.cs)|*.cs|Haskell Files (*.hs)|*.hs|FSharp Files (*.fs)|*.fs";
    let result = dlg.ShowDialog();
    if result.HasValue && result.Value then
        messages.OnNext(OpenFile dlg.FileName)
    ()

let openFolder () =     
    debug "open a folder"
    ()

let saveFile () = 
    debug "save a file"
    ()

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
                        Grid ([GridLength(1.,GridUnitType.Star)],
                              [GridLength(2.,GridUnitType.Star);GridLength(5.);GridLength(1.,GridUnitType.Star)],
                            [
                                Row(Tab tabs,0)
                                Row(Splitter Horizontal,1)
                                Row(Scroll(TextArea state.consoleOutput),2)
                            ]),2)
                ])
    ]

let haskellSyntax = 
    use reader = new XmlTextReader(@"FS-Mode.xshd")
    ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);

let rec render ui : UIElement = 
    match ui with
        | Dock xs -> 
            let d = new DockPanel(LastChildFill=true)
            for x in xs do d.Children.Add (render x) |> ignore
            d :> UIElement
        | Terminal -> new Terminal() :> UIElement
        | Tab xs -> 
            let d = new TabControl()
            for x in xs do d.Items.Add (render x) |> ignore
            d :> UIElement
        | TabItem (title,e,b) ->
            let ti = new TabItem()
            ti.Content <- render e
            ti.Header <- title
            ti.IsSelected <- true
            ti :> UIElement
        | Menu xs ->
            let m = new Menu()
            for x in xs do m.Items.Add (render x) |> ignore
            m :> UIElement
        | MenuItem (title,xs,actions) -> 
            let mi = Controls.MenuItem(Header=title) 
            for x in xs do mi.Items.Add(render x) |> ignore
            match actions with 
                | [BrowseFile] -> mi.Click |> Observable.subscribe(fun e -> openFile()) |> ignore
                | [SaveFile] -> mi.Click |> Observable.subscribe(fun e -> messages.OnNext(SaveFile)) |> ignore
                | other -> ()
            mi :> UIElement
        | Grid (cols,rows,xs) ->
            let g = new Grid()
            for row in rows do g.RowDefinitions.Add(new RowDefinition(Height=row))
            for col in cols do g.ColumnDefinitions.Add(new ColumnDefinition(Width=col))
            for x in xs do g.Children.Add(render x) |> ignore
            g :> UIElement
        | Docked (e,d) ->
            let elt = render e
            DockPanel.SetDock(elt,d)
            elt
        | Column (e,d) ->
            let elt = render e
            Grid.SetColumn(elt,d)
            elt
        | Row (e,d) ->
            let elt = render e
            Grid.SetRow(elt,d)
            elt
        | Splitter Vertical ->
            let gs = new GridSplitter(VerticalAlignment=VerticalAlignment.Stretch,HorizontalAlignment=HorizontalAlignment.Center,ResizeDirection=GridResizeDirection.Columns,ShowsPreview=true,Width=5.)
            gs :> UIElement
        | Splitter Horizontal ->
            let gs = new GridSplitter(VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Stretch,ResizeDirection=GridResizeDirection.Rows,ShowsPreview=true,Height=5.)
            gs :> UIElement
        | Tree xs ->
            let t = new TreeView()
            for x in xs do t.Items.Add(render x) |> ignore
            t :> UIElement
        | TreeItem (title,xs) ->
            let ti = new TreeViewItem()
            ti.Header <- title
            for x in xs do ti.Items.Add(render x) |> ignore
            ti :> UIElement
        | Editor (doc,pos) ->
            let editor = new TextEditor();
            editor.Document <- doc;
            editor.FontFamily <- FontFamily("Consolas")
            editor.TextChanged |> Observable.subscribe(fun e -> messages.OnNext(TextChanged doc)  ) |> ignore
            editor.SyntaxHighlighting <- haskellSyntax;
//            editor.IsReadOnly <- false;
            editor :> UIElement
        | TextArea s -> 
            let tb = new TextBlock()
            tb.Text <- s
            tb.FontFamily <- FontFamily("Consolas")
            tb :> UIElement
        | Scroll e ->
            let scroll = new ScrollViewer()
            scroll.Content <- render e
            scroll :> UIElement
        | other -> failwith "not handled"

let collToList (coll:UIElementCollection) : UIElement list =
    seq { for c in coll -> c } |> Seq.toList

let itemsToList (coll:ItemCollection) =
    seq { for c in coll -> c :?> UIElement } |> Seq.toList

let rec resolve (prev:Element list) (curr:Element list) (screen:UIElement list) : UIElement list = 
    if prev = curr then screen 
    else 
        match (prev,curr,screen) with
            | (x::xs,y::ys,z::zs) when x = y -> z::resolve xs ys zs
            | ((TabItem (ta,ea,ba))::xs,(TabItem (tb,eb,bb))::ys,z::zs) when ea = eb -> 
                let ti = z :?> TabItem
                ti.Header <- tb
                ti.IsSelected <- true
                z::resolve xs ys zs
            | ((Tab a)::xs,(Tab b)::ys,z::zs) -> 
                let tab = z :?> TabControl     
                let childrens = (itemsToList tab.Items)
                tab.Items.Clear()
                for c in resolve a b childrens do 
                    tab.Items.Add(c) |> ignore
                (tab :> UIElement)::resolve xs ys zs
            | ((Dock a)::xs,(Dock b)::ys,z::zs) -> 
                let dock = z :?> DockPanel     
                let childrens = (collToList dock.Children)
                dock.Children.Clear()
                for c in resolve a b childrens do dock.Children.Add(c) |> ignore
                (dock :> UIElement)::resolve xs ys zs
            | ((Column (a,pa))::xs,(Column (b,pb))::ys,z::zs) when pa = pb -> resolve [a] [b] [z] @ resolve xs ys zs
            | ((Row (a,pa))::xs,(Row (b,pb))::ys,z::zs) when pa = pb -> resolve [a] [b] [z] @ resolve xs ys zs
            | ((Grid (acols,arows,a))::xs,(Grid (bcols,brows,b))::ys,z::zs) when acols = bcols && arows = brows -> 
                let grid = z :?> Grid     
                let childrens = (collToList grid.Children)
                grid.Children.Clear()
                for c in resolve a b childrens do grid.Children.Add(c) |> ignore
                (grid :> UIElement)::resolve xs ys zs
            | ((Editor (tda,pa))::xs,(Editor (tdb,pb))::ys,z::zs) when tda = tdb -> 
                z::resolve xs ys zs
            | ((Scroll a)::xs,(Scroll b)::ys,z::zs) -> 
                let scroll = z :?> ScrollViewer
                scroll.Content <- List.head <| resolve [a] [b] [scroll.Content :?> UIElement]
                scroll.ScrollToBottom()
                (scroll:>UIElement)::resolve xs ys zs
            | ((TextArea a)::xs,(TextArea b)::ys,z::zs)  -> 
                let tb = z :?> TextBlock
                // Yeah todo, this is highlyt inefficent
                tb.Text <- b
                z::resolve xs ys zs
            | ([],y::ys,[]) -> (render y)::resolve [] ys []
            | ([],[],[]) -> []
            | (_,y::ys,_) -> 
                failwith <| sprintf "unable to reuse from %A" y
                (render y)::resolve [] ys []
            | other -> failwith <| sprintf "not handled:\n%A" other

// elm-make main.elm --yes
let intialState = {openFiles=[];watches=[("cd C:\Users\Laurent\Documents\code\like; elm-make main.elm --yes")];consoleOutput=""}


let myHost = new MyHost(fun s -> 
    Application.Current.Dispatcher.InvokeAsync(
        fun () ->
            debug s
            messages.OnNext <| CommandOutput s) |> ignore
    )
let myRunSpace = RunspaceFactory.CreateRunspace(myHost);
myRunSpace.Open();

let run (script:string) = 
    Task.Run 
        (fun () ->
            use powershell = PowerShell.Create();
            powershell.Runspace <- myRunSpace;
            powershell.AddScript(script) |> ignore
            powershell.AddCommand("out-default") |> ignore
            powershell.Commands.Commands.[0].MergeMyResults(PipelineResultTypes.Error, PipelineResultTypes.Output);
            powershell.Invoke()
            )





//type App = XAML<"App.xaml">

[<STAThread>]
[<EntryPoint>]
let main argv =
    

    let w =  new Window(Title="F# is fun!",Width=260., Height=420.)
    w.Show()
    w.Content <- List.head <| resolve [] [ui intialState] [] 

    let textChanged = messages.Where(function | TextChanged _ -> true | _ -> false).Throttle(TimeSpan.FromSeconds(2.),DispatcherScheduler.Current)
    //let commandOutputs = messages.Where(function | CommandOutput s -> true | _ -> false).Buffer(TimeSpan.FromSeconds(2.),DispatcherScheduler.Current).Select(fun b -> CommandOutputBuffered b )
    let optMessages = messages.Where(function | TextChanged _ -> false | _ -> true).Merge(textChanged)

    optMessages
        .Scan(intialState,
            fun state cmd ->
                match cmd with 
                | TextChanged doc ->
                    let starize (t:String) = if t.EndsWith("*") then t else t+"*"
                    for cmd in state.watches do run cmd |> ignore
                    {state with openFiles= List.map(fun (t,d,p,b) -> if d = doc then (starize t,d,p,b) else (t,d,p,b)) state.openFiles }      
                | OpenFile s ->
                    let content = IO.File.ReadAllText(s)
                    let doc = new ICSharpCode.AvalonEdit.Document.TextDocument(content)
                    {state with openFiles=state.openFiles@[(IO.Path.GetFileName(s),doc,0,true)] } 
                | SaveFile -> 
                    debug "save"
                    let unstarize (s:String) = s.Replace("*","")
                    let (t,doc,p,b) = List.head state.openFiles
                    {state with openFiles= List.map(fun (t,d,p,b) -> if d = doc then (unstarize t,d,p,b) else (t,d,p,b)) state.openFiles }      
                | CommandOutput s ->
                    {state with consoleOutput = state.consoleOutput+"\n"+s}
                | other -> state  )
        .Scan((ui intialState,ui intialState), fun (prevdom,newdom) state -> (newdom,ui state) )
        .Subscribe(function (p,c) -> w.Content <- List.head <| resolve [p] [c] [downcast w.Content]  )
        |>ignore


    let app = new Application()
    app.Run(w)
//    App().Root.Run()