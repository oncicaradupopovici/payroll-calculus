﻿namespace DataStructures

//[<AutoOpen>]
//module Results =

//    [<RequireQualifiedAccess>]
//    module List =
//        let traverseResult f list =
//            let pure' = Result.Ok
//            let (<*>) fn = Result.bind (fun x-> Result.map (fun f -> f x) fn) 
//            let cons head tail = head :: tail  
//            let initState = pure' []
//            let folder head tail = pure' cons <*> (f head) <*> tail
//            List.foldBack folder list initState

//        let sequenceResult list = traverseResult id list
