module Wpf
open Dom
open System.Windows
open System.Windows.Media
open ICSharpCode.AvalonEdit.Highlighting
open System.Windows.Controls
open ICSharpCode.AvalonEdit
open ICSharpCode.AvalonEdit.Search
open ICSharpCode.AvalonEdit.Folding
open System

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



let collToList (coll:UIElementCollection) : UIElement list =
    seq { for c in coll -> c } |> Seq.toList

let itemsToList (coll:ItemCollection) =
    seq { for c in coll -> c :?> UIElement } |> Seq.toList


let rec render ui : UIElement = 

    let bgColor = new SolidColorBrush(Color.FromRgb (byte 39,byte 40,byte 34))
    let fgColor = new SolidColorBrush(Color.FromRgb (byte 248,byte 248,byte 242))

    match ui with
        | Dock xs -> 
            let d = new DockPanel(LastChildFill=true)
            for x in xs do d.Children.Add (render x) |> ignore
            d :> UIElement
//        | Terminal -> new Terminal() :> UIElement
        | Tab xs -> 
            let d = new TabControl()   
            MahApps.Metro.Controls.TabControlHelper.SetIsUnderlined(d,true);
            for x in xs do d.Items.Add (render x) |> ignore
            d :> UIElement
        | TabItem {title=title;element=e;onSelected=com;onClose=close;selected=selected} ->
            let ti = new TabItem()
            ti.Content <- render e
            let closeButton = new MyEdit.Wpf.Controls.TabItem()
            closeButton.TabTitle.Text <- title
            match close with
                | Some(close) -> closeButton.TabClose.MouseDown |> Observable.subscribe(fun e -> close()) |> ignore
                | Option.None -> ()
            ti.Header <- closeButton
            ti.IsSelected <- selected
            ti :> UIElement
        | Menu xs ->
            let m = new Menu()
            for x in xs do m.Items.Add (render x) |> ignore
            m :> UIElement
        | MenuItem {title=title;elements=xs;onClick=actions;gesture=gestureText} -> 
            let mi = Controls.MenuItem(Header=title) 
            mi.InputGestureText <- gestureText
            for x in xs do mi.Items.Add(render x) |> ignore
            match actions with 
                | Some action -> mi.Click |> Observable.subscribe(fun e -> action()) |> ignore
                | Option.None -> ()
            mi :> UIElement
        | Grid (cols,rows,xs) ->
            let g = new Grid()
            let sizeToLength = function
                | Star n -> GridLength(n,GridUnitType.Star)
                | Pixels n -> GridLength(n)
            for row in rows do g.RowDefinitions.Add(new RowDefinition(Height=sizeToLength row))
            for col in cols do g.ColumnDefinitions.Add(new ColumnDefinition(Width=sizeToLength col))
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
            gs.Background <- (color "#252525").GetBrush(null)
            gs :> UIElement
        | Splitter Horizontal ->
            let gs = new GridSplitter(VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Stretch,ResizeDirection=GridResizeDirection.Rows,ShowsPreview=true,Height=5.)
            gs.Background <- (color "#252525").GetBrush(null)
            gs :> UIElement
        | Tree xs ->
            let t = new TreeView()
            for x in xs do t.Items.Add(render x) |> ignore
            t :> UIElement
        | TreeItem {title=title;elements=xs;onTreeItemSelected=onTreeItemSelected} ->
            let ti = new TreeViewItem()
            ti.Header <- title
            match onTreeItemSelected with
                | Some(action) -> ti.Selected |> Observable.subscribe(fun e -> action()) |> ignore
                | None -> ()
            for x in xs do ti.Items.Add(render x) |> ignore
            ti :> UIElement
        | Editor {doc=doc;selection=selection;textChanged=textChanged} ->
            let editor = new TextEditor();

            editor.SyntaxHighlighting <- HighlightingManager.Instance.GetDefinitionByExtension(IO.Path.GetExtension(doc.FileName));
            editor.Document <- doc;
            editor.FontFamily <- FontFamily("Consolas")
            editor.TextChanged |> Observable.subscribe(fun e -> textChanged(doc)  ) |> ignore
            editor.ShowLineNumbers <- true
            editor.Background <- bgColor
            editor.Foreground <- fgColor
            editor.Options.ConvertTabsToSpaces <- true
            editor.Options.EnableHyperlinks <- false
//            editor.Options.ShowColumnRuler <- true
            let sp = SearchPanel.Install(editor)
            let brush = colors.["FindHighlight"]
            sp.MarkerBrush  <- brush.GetBrush(null)


            let foldingManager = FoldingManager.Install(editor.TextArea);
            let foldingStrategy = new XmlFoldingStrategy();
            foldingStrategy.UpdateFoldings(foldingManager, editor.Document);

            editor.Options.EnableRectangularSelection <- true

            editor :> UIElement
        | TextArea {text=s;onTextChanged=textChanged;onReturn=returnKey} -> 
            let tb = new TextBox(Background = bgColor, Foreground = fgColor)
            tb.Text <- s
            tb.FontFamily <- FontFamily("Consolas")
            match textChanged with
                | Some(action) ->  tb.TextChanged |> Observable.subscribe(fun e -> action tb.Text)  |> ignore
                | Option.None -> ()
            match returnKey with
                | Some(action) ->  tb.KeyDown |> Observable.subscribe(fun e -> action tb.Text)  |> ignore
                | Option.None -> ()
            tb :> UIElement
        | Scroll e ->
            let scroll = new ScrollViewer()
            scroll.Content <- render e
            scroll :> UIElement
        | other -> failwith "not handled"


// Goal of this method is to avoid to call render as much as possible and instead reuse as much as already existing WPF controls between 
// virtual dom changes
// Calling render is expensive as it will create new control and trigger reflows
let rec resolve (prev:Element list) (curr:Element list) (screen:UIElement list) : UIElement list = 
    if prev = curr then screen 
    else 
        match (prev,curr,screen) with
            | (x::xs,y::ys,z::zs) when x = y -> z::resolve xs ys zs
            | ((TabItem {title=ta;element=ea;id=ida})::xs,(TabItem {title=tb;element=eb;selected=selb;id=idb})::ys,z::zs) when ida = idb -> 
                let ti = z :?> TabItem
                let header = ti.Header :?> MyEdit.Wpf.Controls.TabItem
                header.TabTitle.Text <- tb
                ti.IsSelected <- selb
                ti.Content <- List.head <| resolve [ea] [eb] [ti.Content :?> UIElement]

                // This won't handle the case where tabs were reordered since ys wont' have chance to go again trought all xs !
                z::resolve xs ys zs
            | ((TabItem {id=ida})::xs,(TabItem {id=idb} as y)::ys,z::zs) when ida <> idb -> resolve xs (y::ys) zs
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
            | ((Editor {doc=tda;selection=sela})::xs,(Editor {doc=tdb;selection=selb})::ys,z::zs)  -> 
                let editor = z :?> TextEditor
                match selb with
                    | [(s,e)] -> editor.Select(s,e)
                    | [] -> editor.SelectionLength <- 0
                    | multiselect -> ()
                z::resolve xs ys zs
            | ((Scroll a)::xs,(Scroll b)::ys,z::zs) -> 
                let scroll = z :?> ScrollViewer
                scroll.Content <- List.head <| resolve [a] [b] [scroll.Content :?> UIElement]
                scroll.ScrollToBottom()
                (scroll:>UIElement)::resolve xs ys zs
            | ((TextArea {text=a})::xs,(TextArea {text=b})::ys,z::zs)  -> 
                let tb = z :?> TextBox
                tb.AppendText("\n"+b)
                z::resolve xs ys zs
            | ((Tree a)::xs,(Tree b)::ys,z::zs) ->
                let tree = z :?> TreeView     
                let childrens = (itemsToList tree.Items)
                tree.Items.Clear()
                for c in resolve a b childrens do tree.Items.Add(c) |> ignore
                (tree :> UIElement)::resolve xs ys zs
            | ((TreeItem {elements=a})::xs,(TreeItem {title=tb;elements=b})::ys,z::zs) ->
                let tree = z :?> TreeViewItem   
                tree.Header <- tb
                let childrens = (itemsToList tree.Items)
                tree.Items.Clear()
                for c in resolve a b childrens do tree.Items.Add(c) |> ignore
                (tree :> UIElement)::resolve xs ys zs
//            | TreeItem of string*Element list
            | ([],y::ys,[]) -> 
                System.Diagnostics.Debug.WriteLine <| sprintf "render %A" y 
                (render y)::resolve [] ys []
            | ([],[],[]) -> []

            // UI element removed
            | (x::xs,[],zs) -> 
                // for z in zs do ??? what should we do when ui element are removed from the UI, right now they should be GCed
                // will need to check w dont have memory leaks, speialy with all the event subscribers that could be still attached
                // scary place here
                []
            
            | (_,y::ys,_) -> 
                failwith <| sprintf "unable to reuse from %A" y
                (render y)::resolve [] ys []
            | other -> failwith <| sprintf "not handled:\nPREV\n%A\nCURR\n%A\nSCREEN\n%A" prev curr screen

