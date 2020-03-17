namespace PayrollCalculus.PublishedLanguage

open NBB.Core.Abstractions
open MediatR
open NBB.Application.DataContracts

module Commands =
    type AddElemDefinition =
        { elemCode: string; metadata: CommandMetadata }
        interface ICommand
        interface IRequest
        interface IMetadataProvider<CommandMetadata> with
            member this.Metadata with get() = this.metadata
      

