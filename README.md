
## Introduction ##
This is an experimental project to illustrate some options for Federating FHIR services. 

It is not intended to be a production solution, but can illustrate execute the expressions as either an extract (for use in search 
expressions) or validate (for use in StructureDefinition constraints)

Key features:
* Search across federated servers
* Provenance resources included
* Aggregated CapabilityStatement
* Registration of servers (via endpoint resource)
* Support for Xml and Json
* configuration to URL for Registry Services (for validation)

Technically the utility is:
* built on the Microsoft .NET (dotnet) platform
* uses the HL7 FHIR reference assemblies
  * *Core* (NuGet packages starting with `Hl7.Fhir.STU3`) - contains the FhirClient and parsers
  * *Specification* (NuGet packages starting with `Hl7.Fhir.Specification.STU3`) - contains the FHIR Validator
  * *FhirPath* (NuGet package `Hl7.FhirPath`) - the FhirPath evaluator, used by the Core and Specification assemblies
  * *Support* (NuGet package `Hl7.Fhir.Support`) - a library with interfaces, abstractions and utility methods that are used by the other packages

## Support 
Issues can be raised in the GitHub repository at [https://github.com/brianpos/fhirpathtester/issues](https://github.com/brianpos/fhirpathtester/issues).
You are welcome to register your bugs and feature suggestions there. 
For questions and broader discussions, use the .NET FHIR Implementers chat on [Zulip][netapi-zulip].

## Contributing ##
I would gladly welcome any contributors!

If you want to participate in this project, I'm using [Git Flow][nvie] for branch management, so please submit your commits using pull requests no on the develop branches mentioned above! 

[fhirpath-spec]: http://hl7.org/fhirpath/
[stu3-spec]: http://www.hl7.org/fhir
[dstu2-spec]: http://hl7.org/fhir/DSTU2/index.html
[netapi-zulip]: https://chat.fhir.org/#narrow/stream/dotnet
[netapi-docu]: http://ewoutkramer.github.io/fhir-net-api/docu-index.html
[nvie]: http://nvie.com/posts/a-successful-git-branching-model/

### GIT branching strategy
- [NVIE](http://nvie.com/posts/a-successful-git-branching-model/)
- Or see: [Git workflow](https://www.atlassian.com/git/workflows#!workflow-gitflow)

## Licensing
HL7®, FHIR® and the FHIR Mark® are trademarks owned by Health Level Seven International, 
registered with the United States Patent and Trademark Office.
