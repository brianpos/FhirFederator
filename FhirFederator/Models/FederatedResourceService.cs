using System;
using System.Collections.Generic;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.WebApi;
using Hl7.Fhir.Utility;
using FhirFederator.Models;

namespace Hl7.DemoFileSystemFhirServer
{
    public class FederatedResourceService : Hl7.Fhir.WebApi.IFhirResourceServiceSTU3
    {
        public FederatedResourceService(List<FederationMember> members)
        {
            _members = members;
        }

        private List<FederationMember> _members;

        public ModelBaseInputs RequestDetails { get; set; }

        public string ResourceName { get; set; }

        public System.Threading.Tasks.Task<Resource> Create(Resource resource, string ifMatch, string ifNoneExist, DateTimeOffset? ifModifiedSince)
        {
            throw new NotImplementedException();
        }

        public System.Threading.Tasks.Task<string> Delete(string id, string ifMatch)
        {
            throw new NotImplementedException();
        }

        public System.Threading.Tasks.Task<Resource> Get(string resourceId, string VersionId, SummaryType summary)
        {
            throw new NotImplementedException();
        }

        public System.Threading.Tasks.Task<CapabilityStatement.ResourceComponent> GetRestResourceComponent()
        {
            throw new NotImplementedException();
        }

        public System.Threading.Tasks.Task<Bundle> InstanceHistory(string ResourceId, DateTimeOffset? since, DateTimeOffset? Till, int? Count, SummaryType summary)
        {
            throw new NotImplementedException();
        }

        public System.Threading.Tasks.Task<Resource> PerformOperation(string operation, Parameters operationParameters, SummaryType summary)
        {
            throw new NotImplementedException();
        }

        public System.Threading.Tasks.Task<Resource> PerformOperation(string id, string operation, Parameters operationParameters, SummaryType summary)
        {
            throw new NotImplementedException();
        }

        public System.Threading.Tasks.Task<Bundle> Search(IEnumerable<KeyValuePair<string, string>> parameters, int? Count, SummaryType summary)
        {
            Bundle result = new Bundle();
            result.Meta = new Meta();
            result.Id = new Uri("urn:uuid:" + Guid.NewGuid().ToString("n")).OriginalString;
            result.Type = Bundle.BundleType.Searchset;
            result.ResourceBase = RequestDetails.BaseUri;
            result.Total = 0;

            // If there was no count provided, we'll just default in a value
            if (!Count.HasValue)
                Count = 40;

            // TODO: Thread the requests...
            // System.Threading.Tasks.Parallel.ForEach(_members, async (member) =>
            foreach (var member in _members)
            {
                try
                {
                    // create a connection with the supported format type
                    FhirClient server = new FhirClient(member.Url);
                    server.PreferCompressedResponses = true;
                    server.PreferredFormat = member.Format;

                    SearchParams sp = new SearchParams();
                    foreach (var item in parameters)
                    {
                        if (item.Key == "_include")
                            sp.Include.Add(item.Value);
                        else
                            sp.Add(item.Key, item.Value);
                    }
                    sp.Count = Count;
                    sp.Summary = summary;
                    Bundle partialResult = server.Search(sp, ResourceName);
                    lock (result)
                    {
                        if (partialResult.Total.HasValue)
                            result.Total += partialResult.Total;
                        foreach (var entry in partialResult.Entry)
                        {
                            result.Entry.Add(entry);
                            entry.Resource.ResourceBase = server.Endpoint;
                            if (entry.Resource.Meta == null)
                                entry.Resource.Meta = new Meta();
                            entry.Resource.Meta.AddExtension("http://hl7.org/fhir/StructureDefinition/extension-Meta.source|3.2", new FhirUri(entry.Resource.ResourceIdentity(entry.Resource.ResourceBase).OriginalString));
                            var prov = member.CreateProvenance();
                            member.WithProvenance(prov, entry.Resource);
                            result.Entry.Add(new Bundle.EntryComponent() { Resource = prov });
                        }
                    }
                }
                catch(FhirOperationException ex)
                {
                    if (ex.Outcome != null)
                        result.Entry.Add(new Bundle.EntryComponent() { Resource = ex.Outcome });
                }
                catch(Exception ex)
                {
                    // some other weirdness went on
                    OperationOutcome oe = new OperationOutcome();
                    oe.Issue.Add(new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Error,
                        Code = OperationOutcome.IssueType.Exception,
                        Diagnostics = ex.Message
                    });
                    result.Entry.Add(new Bundle.EntryComponent() { Resource = oe });
                }
            }

            // TODO:Merge Sort the results?

            // TODO:Mess with the back/next links

            return System.Threading.Tasks.Task.FromResult(result);
        }

        public System.Threading.Tasks.Task<Bundle> TypeHistory(DateTimeOffset? since, DateTimeOffset? Till, int? Count, SummaryType summary)
        {
            throw new NotImplementedException();
        }
    }
}
