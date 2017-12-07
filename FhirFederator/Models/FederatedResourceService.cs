using System;
using System.Collections.Generic;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.WebApi;
using Hl7.Fhir.Utility;
using FhirFederator.Models;
using System.Linq;

namespace Hl7.DemoFileSystemFhirServer
{
    public class FederatedResourceService : Hl7.Fhir.WebApi.IFhirResourceServiceSTU3
    {
        public FederatedResourceService(List<FederationMember> members)
        {
            _members = members;
        }

        private List<FederationMember> _members;

        private FederationMember SelectFederationMember(string resourceId)
        {
            return _members.Where(m => resourceId.StartsWith(m.IdPrefix)).FirstOrDefault();
        }

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
            var member = SelectFederationMember(resourceId);
            if (member != null)
            {
                // we have a processor that can handle this request
                // create a connection with the supported format type
                FhirClient server = new FhirClient(member.Url);
                member.PrepareFhirClient(server);
                string directUri = ResourceIdentity.Build(server.Endpoint, ResourceName, resourceId.Substring(member.IdPrefix.Length), VersionId).OriginalString;
                try
                {
                    System.Diagnostics.Trace.WriteLine($"Get {resourceId} from {member.Url} {member.Name}");
                    Resource result = server.Get(directUri);
                    result.ResourceBase = server.Endpoint;
                    member.RewriteIdentifiers(result, RequestDetails.BaseUri, directUri);
                    // if there was no valid source extension created (for whatever reason) just record the direct URI in there
                    if (result.Meta.GetExtension("http://hl7.org/fhir/StructureDefinition/extension-Meta.source|3.2") == null)
                        result.Meta.AddExtension("http://hl7.org/fhir/StructureDefinition/extension-Meta.source|3.2", new FhirUri(directUri));
                    return System.Threading.Tasks.Task.FromResult<Resource>(result);
                }
                catch (FhirOperationException ex)
                {
                    if (ex.Outcome != null)
                    {
                        ex.Outcome.Issue.Insert(0, new OperationOutcome.IssueComponent()
                        {
                            Severity = OperationOutcome.IssueSeverity.Information,
                            Code = OperationOutcome.IssueType.Exception,
                            Details = new CodeableConcept(null, null, $"Exception GET {directUri} from {member.Name}"),
                            Diagnostics = member.Url
                        });
                        return System.Threading.Tasks.Task.FromResult<Resource>(ex.Outcome);
                    }
                }
                catch (Exception ex)
                {
                    // some other weirdness went on
                    OperationOutcome oe = new OperationOutcome();
                    oe.Issue.Add(new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Information,
                        Code = OperationOutcome.IssueType.Exception,
                        Details = new CodeableConcept(null, null, $"Exception GET {directUri} from {member.Name}"),
                        Diagnostics = member.Url
                    });
                    oe.Issue.Add(new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Error,
                        Code = OperationOutcome.IssueType.Exception,
                        Diagnostics = ex.Message
                    });
                    return System.Threading.Tasks.Task.FromResult<Resource>(oe);
                }
            }
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
            // No way to select which federation member, so drop it here
            // Unless we allocated specific servers for specific tasks, such as a terminology server
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
                    member.PrepareFhirClient(server);
                    System.Diagnostics.Trace.WriteLine($"Searching {member.Url} {member.Name}");

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
                    // Bundle partialResult = server.Search(sp, ResourceName);
                    Bundle partialResult = server.Search(sp, ResourceName);
                    lock (result)
                    {
                        if (partialResult.Total.HasValue)
                            result.Total += partialResult.Total;
                        foreach (var entry in partialResult.Entry)
                        {
                            result.Entry.Add(entry);
                            entry.Resource.ResourceBase = server.Endpoint;
                            member.RewriteIdentifiers(entry.Resource, RequestDetails.BaseUri, entry.FullUrl);
                            var prov = member.CreateProvenance(entry.Resource, entry.FullUrl);
                            entry.FullUrl = member.RewriteFhirUri(new FhirUri(entry.FullUrl), RequestDetails.BaseUri);
                            result.Entry.Add(new Bundle.EntryComponent()
                            {
                                FullUrl = $"urn-uuid{Guid.NewGuid().ToString("D")}",
                                Search = new Bundle.SearchComponent() { Mode = Bundle.SearchEntryMode.Include },
                                Resource = prov
                            });
                        }
                        OperationOutcome oe = new OperationOutcome();
                        oe.Issue.Add(new OperationOutcome.IssueComponent()
                        {
                            Severity = OperationOutcome.IssueSeverity.Information,
                            Code = OperationOutcome.IssueType.Informational,
                            Details = new CodeableConcept(null,null, $"Searching {member.Name} found {partialResult.Total} results"),
                            Diagnostics = partialResult.SelfLink?.OriginalString
                        });
                        result.Entry.Add(new Bundle.EntryComponent()
                        {
                            Search = new Bundle.SearchComponent() { Mode = Bundle.SearchEntryMode.Outcome },
                            Resource = oe
                        });
                    }
                }
                catch (FhirOperationException ex)
                {
                    if (ex.Outcome != null)
                    {
                        ex.Outcome.Issue.Insert(0, new OperationOutcome.IssueComponent()
                        {
                            Severity = OperationOutcome.IssueSeverity.Information,
                            Code = OperationOutcome.IssueType.Exception,
                            Details = new CodeableConcept(null, null, $"Exception Searching {member.Name}"),
                            Diagnostics = member.Url
                        });
                        result.Entry.Add(new Bundle.EntryComponent()
                        {
                            Search = new Bundle.SearchComponent() { Mode = Bundle.SearchEntryMode.Outcome },
                            Resource = ex.Outcome
                        });
                    }
                }
                catch (Exception ex)
                {
                    // some other weirdness went on
                    OperationOutcome oe = new OperationOutcome();
                    oe.Issue.Add(new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Information,
                        Code = OperationOutcome.IssueType.Exception,
                        Details = new CodeableConcept(null, null, $"Exception Searching {member.Name}"),
                        Diagnostics = member.Url
                    });
                    oe.Issue.Add(new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Error,
                        Code = OperationOutcome.IssueType.Exception,
                        Diagnostics = ex.Message
                    });
                    result.Entry.Add(new Bundle.EntryComponent()
                    {
                        Search = new Bundle.SearchComponent() { Mode = Bundle.SearchEntryMode.Outcome },
                        Resource = oe
                    });
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
