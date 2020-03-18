namespace PayrollCalculus.PublishedLanguage

open NBB.Core.Abstractions
open MediatR
open NBB.Application.DataContracts

module Commands =
    type AddElemDefinition =
        { ElemCode: string; Metadata: CommandMetadata }
        interface ICommand
        interface IRequest
        interface IMetadataProvider<CommandMetadata> with
            member this.Metadata with get() = this.Metadata
      

