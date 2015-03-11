open System
open System.IO
 
Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
 
if not (File.Exists "paket.exe") then
    let url = "https://github.com/fsprojects/Paket/releases/download/0.26.3/paket.exe"
    use wc = new Net.WebClient() in let tmp = Path.GetTempFileName() in wc.DownloadFile(url, tmp); File.Move(tmp,Path.GetFileName url)
 
 
#r "paket.exe"
 
Paket.Dependencies.Install """
    source https://nuget.org/api/v2
    nuget Unquote
""";;
 
 
#r @"packages\FsCheck\lib\net45\FsCheck.dll"
#r @"packages\Unquote\lib\net40\Unquote.dll"


let explode (s:string) =  
    if s <> null then [for c in s -> c]
    else []

module Parser = 

    type Token = Open of char*int | Close of int | None | Node of Token*Token list*Token | Comment

    type Context =  {
        position:int
        content:char list
    }

    type Parser<'a> = Context -> ('a*Context)
    let bind (parser:Parser<'a>) (cont:'a->Parser<'b>) = (fun context -> 
        let (res,context') = parser context
        (cont res) context')
    let ret a = (fun context -> (a,context) )

    type ParserBuilder() =
        member x.Bind(comp, func) = bind comp func
        member x.Return(value) = ret value

    let parser = new ParserBuilder()

    let parse (s:string) = 
        let rec skipToEndOfLine pos s = 
            match s with
                | x::xs when x = '\n' -> (pos+1, xs)
                | x::xs -> skipToEndOfLine (pos+1) xs
                | [] -> (pos,s)
        let comment context = 
            match context.content with
                | '-'::'-'::xs -> 
                    let (nextpos,nexts) = skipToEndOfLine (context.position+2) xs
                    (Comment,{position=nextpos;content=nexts})
                | _ -> (None,{position=context.position;content=context.content})
        let rec parseOpen context = 
            match context.content with
                | x::xs when x = '(' || x = '{' || x = '['-> (Open (x,context.position),{content=xs;position=context.position+1})
                | x::xs when x = ')' || x = '}' || x = ']' -> (None,context)
                | x::xs -> parseOpen {position=(context.position+1);content=xs}
                | [] -> (None,context)
        let rec parseBody =
            parser {
                let! next = _parse
                match next with
                    | None ->return []
                    | _ ->
                        let! tail = parseBody
                        return next::tail
            }
        and parseClose c context = 
            match context.content with
                | x::xs when x = c -> (Close context.position,{content=xs;position=context.position+1})
                | x::xs when x = '(' && c = ')'  -> (None,context)
                | x::xs when x = '{' && c = '}'  -> (None,context)
                | x::xs when x = '[' && c = ']'  -> (None,context)
                | x::xs -> parseClose c {position=context.position+1;content=xs}
                | [] -> (None,context)
        and _parse  =
            parser {
                let! com = comment
                let! left = parseOpen
                let sym = function |'(' -> ')'|'{'-> '}'| '[' -> ']' | _ -> failwith "NA"
                match left with
                    | Open (c,p) ->
                        let! body = parseBody 
                        let! right = parseClose (sym c)
                        return Node(left, body, right)
                    | _ ->return None
            }


        let (res,_) = parseBody {position=0;content=(explode s)}
        res

    let findMatch s pos = 
        let tree = parse s
        let rec _findMatch tree pos = 
            match tree with
                | Node (Open (_,a), xxs, Close b)::xs when a = pos -> Some(b)
                | Node (Open (_,a), xxs, Close b)::xs when b = pos -> Some(a)
                | Node (Open (_,a), xxs, Close b)::xs when pos > a && pos < b -> _findMatch xxs pos
                | x::xs -> _findMatch xs pos
                | [] -> Option.None
        _findMatch tree pos
    

open Parser      
open Swensen.Unquote
open FsCheck

test <@ parse ""  = [] @>
test <@ parse "-- ()"  = [] @>
test <@ parse "()" = [Node (Open ('(',0), [], Close 1)] @>
test <@ parse "()()" = [Node (Open ('(',0),[],Close 1); Node (Open ('(', 2),[],Close 3)] @>
test <@ parse "(abcd)" = [Node (Open ('(',0), [], Close 5)] @>
test <@ parse "() -- comment" = [Node (Open ('(',0), [], Close 1)] @>
test <@ parse """-- comment  
()""" = [Node (Open ('(',13), [], Close 14)] @>
test <@ parse """-- (comment ) 
()""" = [Node (Open ('(',15), [], Close 16)] @>
test <@ parse "(()" = [Node (Open ('(',0), [Node (Open ('(',1),[],Close 2)], None)] @>
test <@ parse "(())" = [Node (Open ('(',0), [Node (Open ('(',1),[],Close 2)], Close 3)] @>
test <@ parse "(()())" = [Node (Open ('(',0), [Node (Open ('(',1),[],Close 2); Node (Open ('(',3),[],Close 4)],Close 5)] @>

test <@findMatch "()" 0 = (Some 1) @>
test <@findMatch "[]" 0 = (Some 1) @>
test <@findMatch "(())" 0 = (Some 3) @>
test <@findMatch "()()" 2 = (Some 3) @>
test <@findMatch "(stuff())" 0 = (Some 8) @>
test <@findMatch "(()" 0 = Option.None @>
test <@findMatch "(())" 1 = (Some 2) @>
test <@findMatch ")(" 1 = Option.None @>
test <@findMatch "blabla()" 0 = Option.None @>
test <@findMatch "()" 1 = (Some 0) @>


let willNeverFindMatchInEmptyList (e:int) = findMatch "" e = Option.None
Check.Quick willNeverFindMatchInEmptyList

type SymParens = SymParens of string with
  static member op_Explicit(SymParens s) = s

type MyGenerators =
  static member SymParens() =
      {new Arbitrary<SymParens>() with
          override x.Generator = gen {
                let! i = Gen.choose(1,100)
                let a = List.replicate i "("
                let b = List.replicate i ")"
                return SymParens (Seq.concat [a;b] |> Seq.reduce (+) )
            }
          override x.Shrinker t = Seq.empty }
        
Arb.register<MyGenerators>()


let willAlwaysFindParensInTheEnd (SymParens e) =  findMatch e 0 = Some(Seq.length e - 1)
Check.Quick willAlwaysFindParensInTheEnd
