open System
open System.IO
 
Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
 
if not (File.Exists "paket.exe") then
    let url = "https://github.com/fsprojects/Paket/releases/download/0.26.3/paket.exe"
    use wc = new Net.WebClient() in let tmp = Path.GetTempFileName() in wc.DownloadFile(url, tmp); File.Move(tmp,Path.GetFileName url)
 
 
#r "paket.exe"
 
Paket.Dependencies.Install """
    source https://nuget.org/api/v2
    nuget FsUnit
    nuget FsUnit.Xunit
    nuget XUnit
    nuget FsCheck
""";;
 
 
#r @"packages\xunit\lib\net20\Xunit.dll"
#r @"packages\FsCheck\lib\net45\FsCheck.dll"
#r @"packages\FsUnit.xUnit\Lib\net40\NHamcrest.dll"
#r @"packages\FsUnit.xUnit\Lib\net40\FsUnit.CustomMatchers.dll"
#r @"packages\FsUnit.xUnit\Lib\net40\FsUnit.Xunit.dll"

open FsUnit.Xunit
open FsCheck

let explode (s:string) =  
    if s <> null then [for c in s -> c]
    else []

module Parser = 
    type Token = Open of char*int | Close of int | None | Node of Token*Token list*Token

    let parse s = 
        let rec skipToEndOfLine pos s = 
            match s with
                | x::xs when x = '\n' -> (pos+1, xs)
                | x::xs -> skipToEndOfLine (pos+1) xs
                | [] -> (pos,s)
        and skipComment pos s = 
            match s with
                | '-'::'-'::xs -> skipToEndOfLine (pos+2) xs
                | _ -> (pos,s)
        and parseOpen pos s = 
            match s with
                | x::xs when x = '(' || x = '{' || x = '['-> (Open (x,pos),xs,pos+1)
                | x::xs when x = ')' || x = '}' || x = ']' -> (None,s,pos)
                | x::xs -> parseOpen (pos+1) xs
                | [] -> (None,s,pos)
        and parseBody pos s =
            let (next,snext,pnext) = _parse pos s
            match next with
                | None ->([],s,pos)
                | _ ->
                    let (tail,stail,ptail) = parseBody pnext snext
                    (next::tail,stail,ptail)
        and parseClose c pos s = 
            match s with
                | x::xs when x = c -> (Close pos,xs,pos+1)
                | x::xs when x = '(' && c = ')'  -> (None,s,pos)
                | x::xs when x = '{' && c = '}'  -> (None,s,pos)
                | x::xs when x = '[' && c = ']'  -> (None,s,pos)
                | x::xs -> parseClose c (pos+1) xs
                | [] -> (None,s,pos)
        and _parse pos s =
            let (coms,comp) = skipComment pos s
            let (left,sa,pa) = parseOpen coms comp
            let sym = function |'(' -> ')'|'{'-> '}'| '[' -> ']' | _ -> failwith "NA"
            match left with
                | Open (c,p) ->
                    let (body,sb,pb) = parseBody pa sa
                    let (right,sc,pc) = parseClose (sym c) pb sb
                    (Node(left, body, right),sc,pc)
                | _ -> (None,s,pos)


        let (res,_,_) = parseBody 0 (explode s)
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
parse ""  |> should equal []
parse "-- ()"  |> should equal []
parse "()" |> should equal <| [Node (Open ('(',0), [], Close 1)]
parse "()()" |> should equal <| [Node (Open ('(',0),[],Close 1); Node (Open 2,[],Close 3)]
parse "(abcd)" |> should equal <| [Node (Open ('(',0), [], Close 5)]
parse "() -- comment" |> should equal <| [Node (Open ('(',0), [], Close 1)]
parse """-- comment 
()""" |> should equal <| [Node (Open ('(',12), [], Close 13)]
parse """-- (comment )
()""" |> should equal <| [Node (Open ('(',14), [], Close 15)]
parse "(()" |> should equal <| [Node (Open ('(',0), [Node (Open ('(',1),[],Close 2)], None)]
parse "(())" |> should equal <| [Node (Open ('(',0), [Node (Open ('(',1),[],Close 2)], Close 3)]
parse "(()())" |> should equal <| [Node (Open ('(',0), [Node (Open ('(',1),[],Close 2);Node (Open ('(',0),[],Close 4)], Close 5)]

findMatch "()" 0 |> should equal (Some 1)
findMatch "[]" 0 |> should equal (Some 1)
findMatch "(())" 0 |> should equal (Some 3)
findMatch "()()" 2 |> should equal (Some 3)
findMatch "(stuff())" 0 |> should equal (Some 8)
findMatch "(()" 0 |> should equal Option.None
findMatch "(())" 1 |> should equal (Some 2)
findMatch ")(" 1 |> should equal Option.None
findMatch "blabla()" 0 |> should equal Option.None
findMatch "()" 1 |> should equal (Some 0)


let willNeverFindMatchInEmptyList (e:int) = findMatch (explode "") e = None
Check.Quick willNeverFindMatchInEmptyList

type SymParens = SymParens of char seq with
  static member op_Explicit(SymParens s) = s

type MyGenerators =
  static member SymParens() =
      {new Arbitrary<SymParens>() with
          override x.Generator = gen {
                let! i = Gen.choose(1,100)
                let a = List.replicate i '('
                let b = List.replicate i ')'
                return SymParens (Seq.concat [a;b])
            }
          override x.Shrinker t = Seq.empty }
        
Arb.register<MyGenerators>()


let willAlwaysFindParensInTheEnd (SymParens e) = 
    printfn "input length %d" (Seq.length e)
    findMatch e 0 = Some(Seq.length e - 1)
Check.Quick willAlwaysFindParensInTheEnd
