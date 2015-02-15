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

//#I "c:/Program Files/Reference Assemblies/Microsoft/Framework/v3.0"
//#I "C:/WINDOWS/Microsoft.NET/Framework/v3.0/WPF/"
//#r "PresentationCore.dll"
//#r "PresentationFramework.dll"
//#r "WindowsBase.dll"
//#r @"System.Xaml"

// #region This is to have wpf keyboqrd input working while running in fsi
module WPFEventLoop =     
    open System    
    open System.Windows    
    open System.Windows.Threading    
    open Microsoft.FSharp.Compiler.Interactive    
    open Microsoft.FSharp.Compiler.Interactive.Settings    

    type RunDelegate<'b> = delegate of unit -> 'b     
    let Create() =         
        let app  =             
            try                 
                // Ensure the current application exists. This may fail, if it already does.                
                let app = new Application() in                 
                // Create a dummy window to act as the main window for the application.                
                // Because we're in FSI we never want to clean this up.                
                new Window() |> ignore;                 
                app              
            with :? InvalidOperationException -> Application.Current        
        let disp = app.Dispatcher        
        let restart = ref false        
        { new IEventLoop with             
            member x.Run() =                    
                app.Run() |> ignore                 
                !restart             

            member x.Invoke(f) =                  
                try 
                    disp.Invoke(DispatcherPriority.Send,new RunDelegate<_>(fun () -> box(f ()))) |> unbox                 
                with e -> eprintf "\n\n ERROR: %O\n" e; reraise()             

            member x.ScheduleRestart() =   ()                 
            //restart := true;                 
            //app.Shutdown()        
         }     

    let Install() = fsi.EventLoop <-  Create()

WPFEventLoop.Install()
// #endregion

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
    | Editor of TextDocument
    | TabItem of (String*Element)
    | Tab of Element list


type EditorState = {
    openFiles:(string*TextDocument) list
    watches : string list
}


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
    printfn "open a folder"
    ()

let saveFile () = 
    printfn "save a file"
    ()

let ui (state:EditorState) = 
    let tabs = state.openFiles |> List.map (fun (t,p) -> TabItem (t,Editor p) ) 
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
                                Row(Terminal,2)
                            ]),2)
                ])
    ]

let haskellSyntax = 
    use reader = new XmlTextReader(System.IO.Path.Combine( __SOURCE_DIRECTORY__ ,@"Syntax\FS-Mode.xshd"))
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
        | TabItem (title,e) ->
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
        | Editor doc ->
            let editor = new TextEditor();
            editor.Document <- doc;
            editor.FontFamily <- FontFamily("Consolas")
            editor.TextChanged |> Observable.subscribe(fun e -> messages.OnNext(TextChanged doc)  ) |> ignore
            editor.SyntaxHighlighting <- haskellSyntax;
//            editor.IsReadOnly <- false;
            editor :> UIElement
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
            | ((TabItem (ta,ea))::xs,(TabItem (tb,eb))::ys,z::zs) when ea = eb -> 
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
            | ((Editor tda)::xs,(Editor tdb)::ys,z::zs) when tda = tdb -> 
                z::resolve xs ys zs
            | ([],y::ys,[]) -> (render y)::resolve [] ys []
            | ([],[],[]) -> []
            | (_,y::ys,_) -> 
                printfn "unable to reuse from %A" y
                (render y)::resolve [] ys []
            | other -> failwith <| sprintf "not handled:\n%A" other

let intialState = {openFiles=[];watches=["elm-make main.elm --yes"]}

let w =  new Window(Title="F# is fun!",Width=260., Height=420., Topmost = true)
w.Show()
w.Content <- List.head <| resolve [] [ui intialState] [] 

let run (cmd:string) = 
    System.Diagnostics.Process.Start(cmd);


messages.Scan(intialState,
            fun state cmd ->
                match cmd with 
                | TextChanged doc ->
                    let starize (t:String) = if t.EndsWith("*") then t else t+"*"
                    for cmd in state.watches do run cmd |> ignore
                    {state with openFiles= List.map(fun (t,d) -> if d = doc then (starize t,d) else (t,d)) state.openFiles }      
                | OpenFile s ->
                    let content = IO.File.ReadAllText(s)
                    let doc = new ICSharpCode.AvalonEdit.Document.TextDocument(content)
                    {state with openFiles=state.openFiles@[(IO.Path.GetFileName(s),doc)] } 
                | SaveFile -> 
                    printfn "save"
                    let unstarize (s:String) = s.Replace("*","")
                    let (t,doc) = List.head state.openFiles
                    {state with openFiles= List.map(fun (t,d) -> if d = doc then (unstarize t,d) else (t,d)) state.openFiles }      
                | other -> state  )
        .Scan((ui intialState,ui intialState), fun (prevdom,newdom) state -> (newdom,ui state) )
        .Subscribe(function (p,c) -> w.Content <- List.head <| resolve [p] [c] [downcast w.Content]  )




                








