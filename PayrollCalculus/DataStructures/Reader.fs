namespace DataStructures

type Reader<'s, 't> = 's -> 't
module Reader =
    let run (x: Reader<'s, 't>) : 's -> 't = 
        x 
    let map (f: 't->'u) (m : Reader<'s, 't>) : Reader<'s,'u> = 
        m >> f
    let bind (f: 't-> Reader<'s, 'u>) (m : Reader<'s, 't>) : Reader<'s, 'u> = 
        fun s -> let a = run m s in run (f a) s
    let apply (f: Reader<'s, ('t -> 'u)>) (m: Reader<'s, 't>) : Reader<'s, 'u> = 
        fun s -> let f = run f s in let a = run m s in f a

    let ask : Reader<'s, 's> = 
        id   

    let pure' x = fun _ -> x


module ReaderBulder =
    type ReaderBulder() =
        member _.Bind (m, f) = Reader.bind f m                    : Reader<'s,'u>
        member _.Return x = Reader.pure' x                        : Reader<'s,'u>
        member _.ReturnFrom x = x                                 : Reader<'s,'u>
        member _.Combine (m1, m2) = Reader.bind (fun _ -> m1) m2  : Reader<'s,'u>
        member _.Zero () = Reader.pure' ()                        : Reader<'s, unit>

[<AutoOpen>]
module Readers =
    let state = new ReaderBulder.ReaderBulder()

    let (<!>) = Reader.map
    let (<*>) = Reader.apply
    let (>>=) st func = Reader.bind func st