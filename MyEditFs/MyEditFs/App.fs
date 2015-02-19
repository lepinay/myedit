module main

open System
open FsXaml

open System
open System.Windows          
open System.Linq
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
open System.Windows.Input
open ICSharpCode.AvalonEdit.Folding
open System.Diagnostics
open ICSharpCode.AvalonEdit.Search
open System.Windows.Shapes
open System.Collections.Generic


//#region helpers
type Column = GridLength
type Row = GridLength
type Title = String
type SplitterDirection = Horizontal | Vertical

type EditorElement =
    { doc: TextDocument;
      selection: (int*int)list } 

[<CustomEquality; CustomComparison>]
type TextAreaElement =
    { text: string;
      onReturn : (string -> unit) option
      onTextChanged : (string -> unit) option } 
    override x.Equals(yobj) =
        match yobj with
        | :? TextAreaElement as y -> x.text = y.text
        | _ -> false
 
    override x.GetHashCode() = hash x.text
    interface System.IComparable with
      member x.CompareTo yobj =
          match yobj with
          | :? TextAreaElement as y -> compare x.text y.text
          | _ -> invalidArg "yobj" "cannot compare values of different types"

[<CustomEquality; CustomComparison>]
type TabItemElement =
    { title: string
      selected:bool
      element: Element
      onSelected : (unit -> unit) option
      onClose : (unit -> unit) option } 
    override x.Equals(yobj) =
        match yobj with
        | :? TabItemElement as y -> x.selected = y.selected && x.title = y.title && x.element = y.element
        | _ -> false
 
    override x.GetHashCode() = (hash x.selected) ^^^ (hash x.title) ^^^ (hash x.element)
    interface System.IComparable with
      member x.CompareTo yobj =
          match yobj with
          | :? TabItemElement as y -> compare x.title y.title
          | _ -> invalidArg "yobj" "cannot compare values of different types"

// http://blogs.msdn.com/b/dsyme/archive/2009/11/08/equality-and-comparison-constraints-in-f-1-9-7.aspx
// TODO separate pure UI command from application business commands
and Command =
    | BrowseFile
    | BrowseFolder
    | SaveFile
    | OpenFile of string
    | TextChanged of TextDocument
    | CommandOutput of string
    | DocSelected of TextDocument
    | DocClosed of TextDocument
    | Search of string
    | SearchNext of string
// TODO see before, elements should have their own commands
and 
    Element = 
    | Docked of Element*Dock
    | Column of Element*int
    | Row of Element*int
    | Dock of Element list
    | Menu of Element list
    | MenuItem of Title*Element list*Command list*String
    | Grid of Column list*Row list*Element list
    | Splitter of SplitterDirection
    | Terminal
    | Tree of Element list
    | TreeItem of Title*Element list
    | Editor of EditorElement
    | TabItem of TabItemElement
    | TextArea of TextAreaElement
    | Tab of Element list
    | Scroll of Element

type Directory = 
    | None
    | Directory of string*Directory list*string list

type TabState = {
    path:string
    doc:TextDocument
    search:string
    selectedText:(int*int)list
}

type EditorState = {
    openFiles:TabState list
    current:TextDocument
    watches : (string) list
    consoleOutput : string
    currentFolder: Directory
}


let messages = new Reactive.Subjects.Subject<Command>()
let debug s = messages.OnNext <| CommandOutput s

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

// We could optimize this by diffing the state to the previous state and generate only the updated dom
// like we already do for the Dom to WPF transformation
let ui (state:EditorState) = 
    let filesToTabs docState = 
        TabItem {
            title=IO.Path.GetFileName(docState.path)
            selected = state.current = docState.doc
            element=Dock
                [
                    Docked(TextArea {
                                        text=docState.search
                                        onTextChanged=Some(fun s -> messages.OnNext(Search s))
                                        onReturn=Some(fun s -> messages.OnNext(SearchNext s)) },Dock.Top)
                    Editor {doc=docState.doc;selection=docState.selectedText}]
            onSelected = Some( fun () -> messages.OnNext(DocSelected docState.doc) )
            onClose = Some( fun () -> messages.OnNext(DocClosed docState.doc) )}
    let tabs = 
        state.openFiles 
        |> List.map filesToTabs
    let rec makeTree = function
        | None -> []
        | Directory (p, folders, files) -> 
            let filest = List.map( fun f -> TreeItem(f,[])) files
            let folderst = List.map makeTree folders |> List.concat
            [TreeItem(p,  folderst @ filest )]

    let tree = Tree <| makeTree state.currentFolder
    Dock [Docked(Menu [MenuItem ("File",
                        [
                        MenuItem ("Open file",[], [BrowseFile], "" )
                        MenuItem ("Open folder",[], [BrowseFolder], "")
                        MenuItem ("Save",[], [SaveFile], "Ctrl+S")], [], "")],Dock.Top)
          Grid ([GridLength(1.,GridUnitType.Star);GridLength(5.);GridLength(2.,GridUnitType.Star)],[],[
                    Column(tree,0)
                    Column(Splitter Vertical,1)
                    Column(
                        Grid ([GridLength(1.,GridUnitType.Star)],
                              [GridLength(2.,GridUnitType.Star);GridLength(5.);GridLength(1.,GridUnitType.Star)],
                                [
                                    Row(Tab tabs,0)
                                    Row(Splitter Horizontal,1)
                                    Row(Scroll(TextArea {text = state.consoleOutput;onTextChanged = Option.None;onReturn = Option.None}),2)
                                ]),2)
                ])
    ]

let color s = new SimpleHighlightingBrush(downcast ColorConverter.ConvertFromString(s))
let defaultColor = color "#FF0000"
let colors = 
    [
    "AccessKeywords", defaultColor;
    "AccessModifiers", defaultColor;
    "AddedText", defaultColor;
    "ASPSection", defaultColor;
    "ASPSectionStartEndTags", defaultColor;
    "Assignment", defaultColor;
    "AttributeName", defaultColor;
    "Attributes", defaultColor;
    "AttributeValue", defaultColor;
    "BlockQuote", defaultColor;
    "BooleanConstants", defaultColor;
    "BrokenEntity", defaultColor;
    "CData", defaultColor;
    "Char", defaultColor;
    "Character", defaultColor;
    "CheckedKeyword", defaultColor;
    "Class", color "#A6E22E";
    "Code", defaultColor;
    "Colon", defaultColor;
    "Command", defaultColor;
    "Comment", color "#75715E";
    "CommentTags", defaultColor;
    "CompoundKeywords", defaultColor;
    "Constants", color "#AE81FF";
    "ContextKeywords", defaultColor;
    "ControlFlow", defaultColor;
    "ControlStatements", defaultColor;
    "CurlyBraces", defaultColor;
    "DataTypes", defaultColor;
    "DateLiteral", defaultColor;
    "Digits", color "#AE81FF";
    "DocComment", defaultColor;
    "DocType", defaultColor;
    "Emphasis", defaultColor;
    "Entities", defaultColor;
    "Entity", color "#F92672";
    "EntityReference", defaultColor;
    "ExceptionHandling", defaultColor;
    "ExceptionHandlingStatements", defaultColor;
    "ExceptionKeywords", defaultColor;
    "FileName", defaultColor;
    "FindHighlight", color "#FFE792";
    "FindHighlightForeground", color "#000000";
    "Friend", defaultColor;
    "FunctionCall", defaultColor;
    "FunctionKeywords", defaultColor;
    "GetSetAddRemove", defaultColor;
    "GotoKeywords", defaultColor;
    "Header", defaultColor;
    "Heading", defaultColor;
    "HtmlTag", color "#F92672";
    "Image", defaultColor;
    "IterationStatements", defaultColor;
    "JavaDocTags", defaultColor;
    "JavaScriptGlobalFunctions", defaultColor;
    "JavaScriptIntrinsics", defaultColor;
    "JavaScriptKeyWords", defaultColor;
    "JavaScriptLiterals", defaultColor;
    "JavaScriptTag", color "#F92672";
    "JScriptTag", color "#F92672";
    "JumpKeywords", defaultColor;
    "JumpStatements", defaultColor;
    "Keywords", color "#F92672";
    "KnownDocTags", defaultColor;
    "LineBreak", defaultColor;
    "Link", defaultColor;
    "Literals", defaultColor;
    "LoopKeywords", defaultColor;
    "MethodCall", defaultColor;
    "MethodName", defaultColor;
    "Modifiers", defaultColor;
    "Namespace", defaultColor;
    "NamespaceKeywords", defaultColor;
    "NullOrValueKeywords", defaultColor;
    "NumberLiteral", color "#AE81FF";
    "OperatorKeywords", defaultColor;
    "Operators", defaultColor;
    "OtherTypes", defaultColor;
    "Package", defaultColor;
    "ParameterModifiers", defaultColor;
    "Position", defaultColor;
    "Preprocessor", defaultColor;
    "Property", defaultColor;
    "Punctuation", defaultColor;
    "ReferenceTypeKeywords", defaultColor;
    "ReferenceTypes", defaultColor;
    "Regex", color "#F6AA11";
    "RemovedText", defaultColor;
    "ScriptTag", color "#F92672";
    "SelectionStatements", defaultColor;
    "Selector", defaultColor;
    "Slash", defaultColor;
    "String", color "#66D9EF";
    "StrongEmphasis", defaultColor;
    "Tags", color "#F92672";
    "This", defaultColor;
    "ThisOrBaseReference", defaultColor;
    "TrueFalse", defaultColor;
    "TypeKeywords", defaultColor;
    "UnchangedText", defaultColor;
    "UnknownAttribute", defaultColor;
    "UnknownScriptTag", defaultColor;
    "UnsafeKeywords", defaultColor;
    "Value", defaultColor;
    "ValueTypeKeywords", defaultColor;
    "ValueTypes", defaultColor;
    "Variable", defaultColor;
    "VBScriptTag", defaultColor;
    "Visibility", defaultColor;
    "Void", defaultColor;
    "XmlDeclaration", defaultColor;
    "XmlPunctuation", defaultColor;
    "XmlString", defaultColor;
    "XmlTag", color "#F92672"] 
    |> Map.ofList

type Cross = FsXaml.XAML<"cross.xaml", true>

let rec render (parent:UIElement) ui (dict:Dictionary<Element,UIElement*UIElement>) : UIElement*UIElement = 
    let bgColor = new SolidColorBrush(Color.FromRgb (byte 39,byte 40,byte 34))
    let fgColor = new SolidColorBrush(Color.FromRgb (byte 248,byte 248,byte 242))

    let leave (p:UIElement) c = 
        match p with
        | :? DockPanel as dock -> dock.Children.Remove(c)

    let join (p:UIElement) c = 
        match p with
        | :? DockPanel as dock -> dock.Children.Add(c)


    match dict.TryGetValue(ui) with
        | (true,(cparent,me)) when cparent <> parent -> 
            leave cparent me
            join parent me |> ignore
            dict.[ui] <- (parent,me)
            (parent,me)
        | (true,me)  -> me
        | (false,_) ->  
            match ui with
                | Dock xs as dock -> 
                    let d = new DockPanel(LastChildFill=true)
                
                    for x in xs do 
                        d.Children.Add (render d x dict) |> ignore
                    dict.Add(dock,(parent,d))
                    (parent,d) :> UIElement
                | Terminal as term -> 
                    let t = new Terminal()
                    dict.Add(term,t)
                    t :> UIElement
                | Tab xs as e -> 
                    let d = new TabControl()            
                    for x in xs do d.Items.Add (render x dict) |> ignore
                    dict.Add(e,d)
                    d :> UIElement
                | TabItem {title=title;element=e;onSelected=com;onClose=close;selected=selected} as te->
                    let ti = new TabItem()
                    ti.Content <- render e dict
                    let closeButton = new Cross()
                    closeButton.title.Text <- title
                    closeButton.title.MouseLeftButtonDown
                        |> Observable.subscribe(fun e ->
                            match com with 
                                | Some(tag) -> tag() |> ignore 
                                | Option.None -> ()) |> ignore
                    match close with
                        | Some(close) -> closeButton.closeButton.Click |> Observable.subscribe(fun e -> close()) |> ignore
                        | Option.None -> ()
                    ti.Header <- closeButton
                    ti.IsSelected <- selected
                    dict.Add(te,ti)
                    ti :> UIElement
                | Menu xs as e->
                    let m = new Menu()
                    for x in xs do m.Items.Add (render x dict) |> ignore
                    dict.Add(e,m)
                    m :> UIElement
                | MenuItem (title,xs,actions,gestureText) as e-> 
                    let mi = Controls.MenuItem(Header=title) 
                    mi.InputGestureText <- gestureText
                    for x in xs do mi.Items.Add(render x dict) |> ignore
                    match actions with 
                        | [BrowseFile] -> mi.Click |> Observable.subscribe(fun e -> openFile()) |> ignore
                        | [SaveFile] -> mi.Click |> Observable.subscribe(fun e -> messages.OnNext(SaveFile)) |> ignore
                        | other -> ()
                    dict.Add(e,mi)
                    mi :> UIElement
                | Grid (cols,rows,xs) as e->
                    let g = new Grid()
                    for row in rows do g.RowDefinitions.Add(new RowDefinition(Height=row))
                    for col in cols do g.ColumnDefinitions.Add(new ColumnDefinition(Width=col))
                    for x in xs do g.Children.Add(render x dict) |> ignore
                    dict.Add(e,g)
                    g :> UIElement
                | Docked (e,d) as de ->
                    let elt = render e dict
                    DockPanel.SetDock(elt,d)
                    dict.Add(de,elt)
                    elt
                | Column (e,d) as ce ->
                    let elt = render e dict
                    Grid.SetColumn(elt,d)
                    dict.Add(ce,elt)
                    elt
                | Row (e,d) as re ->
                    let elt = render e dict
                    Grid.SetRow(elt,d)
                    dict.Add(re,elt)
                    elt
                | Splitter Vertical as e ->
                    let gs = new GridSplitter(VerticalAlignment=VerticalAlignment.Stretch,HorizontalAlignment=HorizontalAlignment.Center,ResizeDirection=GridResizeDirection.Columns,ShowsPreview=true,Width=5.)
                    dict.Add(e,gs)
                    gs :> UIElement
                | Splitter Horizontal as e ->
                    let gs = new GridSplitter(VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Stretch,ResizeDirection=GridResizeDirection.Rows,ShowsPreview=true,Height=5.)
                    dict.Add(e,gs)
                    gs :> UIElement
                | Tree xs as e ->
                    let t = new TreeView()
                    for x in xs do t.Items.Add(render x dict) |> ignore
                    dict.Add(e,t)
                    t :> UIElement
                | TreeItem (title,xs) as e ->
                    let ti = new TreeViewItem()
                    ti.Header <- title
                    for x in xs do ti.Items.Add(render x dict) |> ignore
                    dict.Add(e,ti)
                    ti :> UIElement
                | Editor {doc=doc;selection=selection} as e ->
                    let editor = new TextEditor();

                    editor.SyntaxHighlighting <- HighlightingManager.Instance.GetDefinitionByExtension(IO.Path.GetExtension(doc.FileName));
                    editor.Document <- doc;
                    editor.FontFamily <- FontFamily("Consolas")
                    editor.TextChanged |> Observable.subscribe(fun e -> messages.OnNext(TextChanged doc)  ) |> ignore
                    editor.ShowLineNumbers <- true
                    editor.Background <- bgColor
                    editor.Foreground <- fgColor
                    editor.Options.ConvertTabsToSpaces <- true
                    editor.Options.EnableHyperlinks <- false
                    editor.Options.ShowColumnRuler <- true
                    let sp = SearchPanel.Install(editor)
                    let brush = colors.["FindHighlight"]
                    sp.MarkerBrush  <- brush.GetBrush(null)


                    let foldingManager = FoldingManager.Install(editor.TextArea);
                    let foldingStrategy = new XmlFoldingStrategy();
                    foldingStrategy.UpdateFoldings(foldingManager, editor.Document);

                    editor.Options.EnableRectangularSelection <- true
                    dict.Add(e,editor)
                    editor :> UIElement
                | TextArea {text=s;onTextChanged=textChanged;onReturn=returnKey} as e -> 
                    let tb = new TextBox(Background = bgColor, Foreground = fgColor)
                    tb.Text <- s
                    tb.FontFamily <- FontFamily("Consolas")
                    match textChanged with
                        | Some(action) ->  tb.TextChanged |> Observable.subscribe(fun e -> action tb.Text)  |> ignore
                        | Option.None -> ()
                    match returnKey with
                        | Some(action) ->  tb.KeyDown |> Observable.subscribe(fun e -> action tb.Text)  |> ignore
                        | Option.None -> ()
                    dict.Add(e,tb)
                    tb :> UIElement
                | Scroll e as se->
                    let scroll = new ScrollViewer()
                    scroll.Content <- render e dict
                    dict.Add(se,scroll)
                    scroll :> UIElement
                | other -> failwith "not handled"

let collToList (coll:UIElementCollection) : UIElement list =
    seq { for c in coll -> c } |> Seq.toList

let itemsToList (coll:ItemCollection) =
    seq { for c in coll -> c :?> UIElement } |> Seq.toList

// Goal of this method is to avoid to call render as much as possible and instead reuse as much as already existing WPF controls between 
// virtual dom changes
// Calling render is expensive as it will create new control and trigger reflows
let rec resolve (prev:Element option) (curr:Element) (screen:Dictionary<Element,UIElement>) : UIElement = 
    let remap preve curre ti = 
//        screen.Remove(curre) |> ignore
        screen.Remove(preve) |> ignore
        screen.Add(curre,ti)
        ti :> UIElement
    let resolveChildrens (childrens:Element seq) (action:UIElement ->'a) = 
        for c in childrens  do 
            let (found,oldme) = screen.TryGetValue(c)
            if found then action(oldme) |> ignore
            else 
                let newMe = resolve Option.None c screen
                action(newMe) |> ignore

    match (prev,curr) with
        | (Some prev,curr) when prev = curr ->  screen.[prev] 
        | (Some(TabItem {title=ta;element=ea} as preve),(TabItem {title=tb;element=eb;selected=selb} as curre) ) -> 
            let ti = screen.[preve] :?> TabItem
            let header = ti.Header :?> Cross
            header.title.Text <- tb
            ti.IsSelected <- selb
            ti.Content <- resolve (Some(ea)) eb screen
            remap preve curre ti
        | (Some(Tab a as preve) ,(Tab b as curre) ) -> 
            let tab = screen.[preve] :?> TabControl     
            tab.Items.Clear()
            resolveChildrens b tab.Items.Add
            remap preve curre tab
        | (Some(Dock a as preve),(Dock b as curre)) -> 
            let dock = screen.[preve] :?> DockPanel     
            dock.Children.Clear()
            resolveChildrens b dock.Children.Add
            remap preve curre dock
        | (Some(Column (a,pa)),(Column (b,pb))) when pa = pb -> resolve (Some a) b screen
        | (Some(Row (a,pa)),(Row (b,pb))) when pa = pb -> resolve (Some a) b screen
        | (Some(Grid (acols,arows,a) as preve),(Grid (bcols,brows,b) as curre)) when acols = bcols && arows = brows -> 
            let grid = screen.[preve] :?> Grid     
            grid.Children.Clear()
            resolveChildrens b grid.Children.Add
            remap preve curre grid
        | (Some(Editor {doc=tda;selection=sela} as preve),(Editor {doc=tdb;selection=selb} as curre))  -> 
            let editor = screen.[preve] :?> TextEditor
            match selb with
                | [(s,e)] -> editor.Select(s,e)
                | [] -> editor.SelectionLength <- 0
                | multiselect -> ()
            remap preve curre editor
        | (Some(Scroll a as preve),(Scroll b as curre)) -> 
            let scroll = screen.[preve] :?> ScrollViewer
            scroll.Content <- resolve (Some preve) b screen
            scroll.ScrollToBottom()
            remap preve curre scroll
        | (Some(TextArea {text=a} as preve),(TextArea {text=b} as curre))  -> 
            let tb = screen.[preve] :?> TextBox
            tb.AppendText("\n"+b)
            remap preve curre tb
        | (Option.None,y) -> render y screen
            
        | (Some x,y) -> 
            failwith <| sprintf "unable to reuse from %A" x
            render y screen
        | other -> failwith <| sprintf "not handled:\nPREV\n%A\nCURR\n%A\nSCREEN\n%A" prev curr screen

// elm-make main.elm --yes
let intialState = {
    openFiles=[]
    watches=[("elm-make %currentpath% --yes")]
    consoleOutput=""
    current=null
    currentFolder=Directory ("Code", [Directory ("Src", [], ["file1.test";"file2.test"]) ], ["file1";"file2"]) 
    }


//let myHost = new MyHost(fun s -> 
//    Application.Current.Dispatcher.InvokeAsync(
//        fun () ->
//            debug s
//            messages.OnNext <| CommandOutput s) |> ignore
//    )
//let myRunSpace = RunspaceFactory.CreateRunspace(myHost);
//myRunSpace.Open();

let run (script:string) = 
    Task.Run 
        (fun () ->
//            use powershell = PowerShell.Create();
//            powershell.Runspace <- myRunSpace;
//            powershell.AddScript(script) |> ignore
//            powershell.AddCommand("out-default") |> ignore
//            powershell.Commands.Commands.[0].MergeMyResults(PipelineResultTypes.Error, PipelineResultTypes.Output);
//            powershell.Invoke()
              let pi = ProcessStartInfo (
                        FileName = "cmd",
                        Arguments = "/c cd C:\perso\like && elm-make.exe main.elm --yes",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true )
                        
              let proc = new System.Diagnostics.Process()
              proc.StartInfo <- pi

              proc.Start() |> ignore

              while not proc.StandardOutput.EndOfStream do
                let line = proc.StandardOutput.ReadLine()
                messages.OnNext <| CommandOutput line
              while not proc.StandardError.EndOfStream do
                let line = proc.StandardError.ReadLine()
                messages.OnNext <| CommandOutput line
              
            )





//type App = XAML<"App.xaml">
let rec oneBefore files doc = 
    match files with
        | [] -> null
        | [current] when current.doc = doc -> null
        | before::current::xs when current.doc = doc -> before.doc
        | current::next::xs when current.doc = doc -> next.doc
        | x::xs -> oneBefore xs doc
        | [x] -> null

[<STAThread>]
[<EntryPoint>]
let main argv =
    
    let addSyntax (f:string) name ext = 
        use reader = new XmlTextReader(f)
        let customHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);
        HighlightingManager.Instance.RegisterHighlighting(name, ext |> List.toArray, customHighlighting);

    addSyntax @"Elm-Mode.xshd" "Elm"  [".elm"]
    addSyntax @"Html-Mode.xshd" "Html"  [".html";"*.htm"]

    for def in HighlightingManager.Instance.HighlightingDefinitions do
        for c in def.NamedHighlightingColors do 
            c.Foreground <- colors.[c.Name]
    
//                c.Foreground <- new SimpleHighlightingBrush(Color.FromRgb (byte 248,byte 248,byte 242))


    let w =  new Window(Title="F# is fun!",Width=260., Height=420.)
    let screen = Dictionary<Element,UIElement>()
    w.WindowState <- WindowState.Maximized
    w.Show()
    w.Content <- render (ui intialState) screen


    let saveCommand = new KretschIT.WP_Fx.UI.Commands.RelayCommand(fun e -> messages.OnNext(SaveFile)  )
//    let searchCommand = new KretschIT.WP_Fx.UI.Commands.RelayCommand(fun e -> messages.OnNext(Search)  )
    w.InputBindings.Add(new KeyBinding(saveCommand,Key.S,ModifierKeys.Control)) |> ignore
//    w.InputBindings.Add(new KeyBinding(searchCommand,Key.F,ModifierKeys.Control)) |> ignore

    let textChanged = messages.Where(function | TextChanged _ -> true | _ -> false).Throttle(TimeSpan.FromSeconds(1.))
    //let commandOutputs = messages.Where(function | CommandOutput s -> true | _ -> false).Buffer(TimeSpan.FromSeconds(2.),DispatcherScheduler.Current).Select(fun b -> CommandOutputBuffered b )
    let optMessages = messages.Where(function | TextChanged _ -> false | _ -> true).Merge(textChanged)

    optMessages
        .Scan(intialState,
            fun state cmd ->
                match cmd with 
                | TextChanged doc ->
                    let starize (t:String) = if t.EndsWith("*") then t else t+"*"
                    {state with openFiles= List.map(fun tstate -> if tstate.doc = doc then {tstate with path = starize tstate.path} else tstate) state.openFiles }      
                | OpenFile s ->
                    let content = IO.File.ReadAllText(s)
                    let doc = new ICSharpCode.AvalonEdit.Document.TextDocument(content)
                    doc.FileName <- s
                    {state with openFiles=state.openFiles@[{path=s;doc=doc;search="";selectedText=[]}] } 
                | SaveFile -> 
                    let unstarize (s:String) = s.Replace("*","")
                    let tstate = List.find (fun tsate -> tsate.doc = state.current) state.openFiles
                    IO.File.WriteAllText(tstate.doc.FileName,tstate.doc.Text)

                    for cmd in state.watches do 
                        let cwd s = "cd " +  IO.Path.GetDirectoryName tstate.doc.FileName + ";" + s
                        cmd.Replace("%currentpath%", tstate.doc.FileName) |> cwd |> run |> ignore

                    {state with openFiles= List.map(fun tstate' -> if tstate.doc = tstate'.doc then {tstate' with path = unstarize tstate'.path} else tstate') state.openFiles }      
                | CommandOutput s ->
                    {state with consoleOutput = s}
                | DocSelected s ->
                    {state with current = s}
                | DocClosed s ->
                    {state with openFiles = state.openFiles |> List.filter(fun tstate -> tstate.doc <> s ); current = oneBefore state.openFiles s   }
                | Search s ->
                    let res = state.current.Text.IndexOf(s)
                    if res >= 0 then
                        let tstate = List.find (fun tsate -> tsate.doc = state.current) state.openFiles
                        {state with openFiles= List.map (fun tab -> if tab.doc = state.current then {tab with selectedText = [(res,s.Length)] } else tab ) state.openFiles }
                    else state
                | SearchNext s ->
                    let tstate = List.find (fun tsate -> tsate.doc = state.current) state.openFiles
                    match tstate.selectedText with
                        | [(idx,len)] ->
                            let res = state.current.Text.IndexOf(s,idx+len)
                            if res >= 0 then
                                let tstate = List.find (fun tsate -> tsate.doc = state.current) state.openFiles
                                {state with openFiles= List.map (fun tab -> if tab.doc = state.current then {tab with selectedText = [(res,s.Length)] } else tab ) state.openFiles }
                            else state
                        | other -> state
                | other -> 
                    debug <| sprintf "not handled %A" other
                    state  )
        .Scan((ui intialState,ui intialState), fun (prevdom,newdom) state -> (newdom,ui state) )
        .ObserveOnDispatcher()
        .Subscribe(function (p,c) -> w.Content <- render c screen  )
        |>ignore

    let app = new Application()
    app.Run(w)
//    App().Root.Run()