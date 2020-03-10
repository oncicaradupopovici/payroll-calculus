namespace DataStructures

[<RequireQualifiedAccess>]
module Result =
    let inline join (res: Result<Result<'t, 'e>, 'e>) =
       res |> Result.bind id 