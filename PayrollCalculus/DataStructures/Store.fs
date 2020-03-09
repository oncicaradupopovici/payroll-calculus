namespace DataStructures

//type Store<'p, 'a> = ('p -> 'a) * 'p
//module Store =
//    let map (f: 'a->'b) ((lookup, pointer) : Store<'p, 'a>) : Store<'p, 'b> = 
//        (lookup >> f, pointer)

//    let extend (f: Store<'p, 'a> -> 'b) ((lookup, pointer) : Store<'p, 'a>) : Store<'p, 'b> = 
//        ((fun p -> f (lookup, p)), pointer)

//    let extract ((lookup, pointer): Store<'p, 'a>) : 'a =
//        lookup pointer

//    let seek  (p: 'p) ((lookup, _) : Store<'p, 'a>) : Store<'p, 'a> = 
//        (lookup, p)

//    let peek (p: 'p) ((lookup, _) : Store<'p, 'a>)  : 'a = 
//        lookup p

//    let duplicate (wa: Store<'p, 'a>) : Store<'p, Store<'p, 'a>> = 
//        extend id wa

//[<AutoOpen>]
//module Stores =
//    let (<!>) = Store.map
//    let (>=>) (f: Store<'p, 'a> -> 'b) (g: Store<'p, 'b> -> 'c) =
//        (Store.extend f) >> g