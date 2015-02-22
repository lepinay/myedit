module main 

open ICSharpCode.AvalonEdit.Document
open System

open Dom
open Wpf
open System.Windows
open System.Xml
open ICSharpCode.AvalonEdit.Highlighting
open System.Windows.Input
open System
open System.Linq
open System.Windows.Controls  
open System.Reactive.Linq
open System.Threading.Tasks
open System.Diagnostics

type Command =
    | SaveFile
    | OpenFile of string
    | OpenFolder of string
    | TextChanged of TextDocument
    | CommandOutput of string
    | DocSelected of TextDocument
    | DocClosed of TextDocument
    | Search of string
    | SearchNext of string
    | SelectFile of string
    | ExpandFolder of string

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
let debug s = messages.OnNext <| CommandOutput (sprintf "[%A] %s:" DateTime.Now s)

let openFile () = 
    let dlg = new Microsoft.Win32.OpenFileDialog();
    dlg.DefaultExt <- ".cs";
    dlg.Filter <- "All Files (*.*)|*.*|CSharp Files (*.cs)|*.cs|Haskell Files (*.hs)|*.hs|FSharp Files (*.fs)|*.fs";
    let result = dlg.ShowDialog();
    if result.HasValue && result.Value then
        messages.OnNext(OpenFile dlg.FileName)
    ()

let openFolder () =     
    let dialog = new System.Windows.Forms.FolderBrowserDialog();
    match dialog.ShowDialog() with
        | Forms.DialogResult.OK -> messages.OnNext(OpenFolder dialog.SelectedPath)
        | other -> ()

let saveFile () = 
    debug "save a file"
    ()

// We could optimize this by diffing the state to the previous state and generate only the updated dom
// like we already do for the Dom to WPF transformation
let ui (state:EditorState) = 
    let filesToTabs docState = 
        Dom.TabItem {
            id=docState.doc.FileName
            title=IO.Path.GetFileName(docState.path)
            selected = state.current = docState.doc
            element=Dom.Dock
                [
//                    Docked(TextArea {
//                                        text=docState.search
//                                        onTextChanged=Some(fun s -> messages.OnNext(Search s))
//                                        onReturn=Some(fun s -> messages.OnNext(SearchNext s)) },Dock.Top)
                    Editor {doc=docState.doc;selection=docState.selectedText;textChanged=fun doc -> messages.OnNext(TextChanged doc)}]
            onSelected = Some( fun () -> messages.OnNext(DocSelected docState.doc) )
            onClose = Some( fun () -> messages.OnNext(DocClosed docState.doc) )}
    let tabs = 
        state.openFiles 
        |> List.map filesToTabs
    let rec makeTree = function
        | None -> []
        | Directory (p, folders, files) -> 
            let filest = List.map( fun f -> TreeItem{title=f;elements=[];onTreeItemSelected=Some(fun () -> messages.OnNext(SelectFile f))}) files
            let folderst = List.map makeTree folders |> List.concat
            [TreeItem{title=p;elements=folderst @ filest;onTreeItemSelected = Some(fun () ->messages.OnNext( ExpandFolder p))}]

    let tree = Tree <| makeTree state.currentFolder
    Dom.Dock [
        Docked(Dom.Menu [Dom.MenuItem {title="File";gesture= "";onClick= Option.None;elements=[
                Dom.MenuItem {title="Open file";elements=[];onClick=Some(openFile);gesture="" };
                Dom.MenuItem {title="Open folder";elements=[];onClick=Some(openFolder);gesture=""};
                Dom.MenuItem {title="Save";elements=[];onClick=Some(fun () -> messages.OnNext(SaveFile)); gesture="Ctrl+S"}]}],Dock.Top)
        Dom.Grid ([Star 2.;Pixels 1.;Star 8.],[],[
                Dom.Column(tree,0)
                Dom.Column(Splitter Vertical,1)
                Dom.Column(
                    Dom.Grid ([Star 1.],
                            [Star 2.;Pixels 1.;Star 1.],
                            [
                                Row(Tab tabs,0)
                                Row(Splitter Horizontal,1)
                                Row(Scroll(TextArea {text = state.consoleOutput;onTextChanged = Option.None;onReturn = Option.None}),2)
                            ]),2)
            ])
    ]



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

let renderApp (w:Window) =
    
    let addSyntax (f:string) name ext = 
        use reader = new XmlTextReader(f)
        let customHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);
        HighlightingManager.Instance.RegisterHighlighting(name, ext |> List.toArray, customHighlighting);

    addSyntax @"Elm-Mode.xshd" "Elm"  [".elm"]
    addSyntax @"Html-Mode.xshd" "Html"  [".html";"*.htm"]

    for def in HighlightingManager.Instance.HighlightingDefinitions do
        for c in def.NamedHighlightingColors do 
            c.Foreground <- colors.[c.Name]

    w.Content <- List.head <| resolve [] [ui intialState] [] 


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
                    {state with current=doc; openFiles=state.openFiles@[{path=s;doc=doc;search="";selectedText=[]}] } 
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
                | OpenFolder s ->
                    let dirs = 
                        System.IO.Directory.EnumerateDirectories(s)
                        |> Seq.map(fun p -> Directory(p,[],[])  )
                        |> Seq.toList
                    {state with currentFolder=Directory(s,dirs,System.IO.Directory.EnumerateFiles(s) |> Seq.toList) }
                | other -> 
                    Console.WriteLine(sprintf "not handled %A" other)
                    state  )
        .Scan((ui intialState,ui intialState), fun (prevdom,newdom) state -> (newdom,ui state) )
        .ObserveOnDispatcher()
        .Subscribe(function (p,c) -> w.Content <- List.head <| resolve [p] [c] [downcast w.Content]  )
        |>ignore

