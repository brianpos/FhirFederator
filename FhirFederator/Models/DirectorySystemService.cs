using System;
using System.Collections.Generic;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.WebApi;
using System.Threading.Tasks;
using FhirFederator.Models;
using System.Linq;

namespace Hl7.DemoFileSystemFhirServer
{
    /// <summary>
    /// This is an implementation of the FHIR Service that sources all its files in the file system
    /// </summary>
    public class DirectorySystemService : Hl7.Fhir.WebApi.IFhirSystemServiceSTU3
    {
        public DirectorySystemService()
        {
            InitializeIndexes();
        }

        /// <summary>
        /// The File system directory that will be scanned for the storage of FHIR resources
        /// </summary>
        public static string Directory { get; set; }

        public void InitializeIndexes()
        {
        }

        public List<FederationMember> Members()
        {
            List<FederationMember> members = new List<FederationMember>();

            // read these from the file system
            var parser = new Fhir.Serialization.FhirXmlParser();
            var files = System.IO.Directory.EnumerateFiles(DirectorySystemService.Directory, $"Endpoint.*.xml").ToList();
            files.Sort();
            foreach (var filename in files)
            {
                var resource = parser.Parse<Endpoint>(System.IO.File.ReadAllText(filename));
                members.Add(new FederationMember(resource));
            }

            return members;
        }

        public Task<CapabilityStatement> GetConformance(ModelBaseInputs request, SummaryType summary)
        {
            Hl7.Fhir.Model.CapabilityStatement con = new Hl7.Fhir.Model.CapabilityStatement();
            con.Url = request.BaseUri + "metadata";
            con.Description = new Markdown("Demonstration Directory based FHIR server");
            con.DateElement = new Hl7.Fhir.Model.FhirDateTime("2017-04-30");
            con.Version = "1.0.0.0";
            con.Name = "";
            con.Experimental = true;
            con.Status = PublicationStatus.Active;
            con.FhirVersion = Hl7.Fhir.Model.ModelInfo.Version;
            con.AcceptUnknown = CapabilityStatement.UnknownContentCode.Extensions;
            con.Format = new string[] { "xml", "json" };
            con.Kind = CapabilityStatement.CapabilityStatementKind.Instance;
            con.Meta = new Meta();
            con.Meta.LastUpdatedElement = Instant.Now();

            con.Rest = new List<Hl7.Fhir.Model.CapabilityStatement.RestComponent>
            {
                new Hl7.Fhir.Model.CapabilityStatement.RestComponent()
                {
                    Operation = new List<Hl7.Fhir.Model.CapabilityStatement.OperationComponent>()
                }
            };
            con.Rest[0].Mode = CapabilityStatement.RestfulCapabilityMode.Server;
            con.Rest[0].Resource = new List<Hl7.Fhir.Model.CapabilityStatement.ResourceComponent>();

            //foreach (var model in ModelFactory.GetAllModels(GetInputs(buri)))
            //{
            //    con.Rest[0].Resource.Add(model.GetRestResourceComponent());
            //}
            foreach (var member in Members())
            {
                try
                {
                    // create a connection with the supported format type
                    FhirClient server = new FhirClient(member.Url);
                    member.PrepareFhirClientSecurity(server);
                    System.Diagnostics.Trace.WriteLine($"Retrieving CapabilityStatement {member.Url} {member.Name}");
                    server.PreferCompressedResponses = true;
                    server.PreferredFormat = member.Format;

                    CapabilityStatement csMember = server.CapabilityStatement();
                    if (con.Rest[0].Resource.Count == 0)
                    {
                        // just clone all the resources from this one!
                        // a great start
                        foreach (var item in csMember.Rest?.FirstOrDefault()?.Resource)
                        {
                            item.AddExtension("http://example.org/Federation-member-name", new FhirString(member.Name));
                            con.Rest[0].Resource.Add(item);

                            // remove the non supported actions
                            item.ConditionalCreate = null;
                            item.ConditionalUpdate = null;
                            item.ConditionalDelete = null;
                            item.UpdateCreate = null;
                            if (item.Type != ResourceType.Endpoint)
                            {
                                item.Interaction.RemoveAll(i =>
                                    i.Code == CapabilityStatement.TypeRestfulInteraction.Create
                                    || i.Code == CapabilityStatement.TypeRestfulInteraction.Update
                                    || i.Code == CapabilityStatement.TypeRestfulInteraction.Delete
                                    );
                            }
                        }
                    }
                    else
                    {
                        // Tag all these with others
                        foreach (var item in con.Rest?.FirstOrDefault()?.Resource)
                        {
                            if (csMember.Rest[0].Resource.Where(c => c.Type == item.Type).Any())
                            {
                                item.AddExtension("http://example.org/Federation-member-name", new FhirString(member.Name));
                            }
                        }
                    }
                }
                catch (FhirOperationException ex)
                {
                    System.Diagnostics.Trace.WriteLine(ex.Message);
                    //if (ex.Outcome != null)
                    //    result.Entry.Add(new Bundle.EntryComponent()
                    //    {
                    //        Search = new Bundle.SearchComponent() { Mode = Bundle.SearchEntryMode.Outcome },
                    //        Resource = ex.Outcome
                    //    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine(ex.Message);
                    //// some other weirdness went on
                    //OperationOutcome oe = new OperationOutcome();
                    //oe.Issue.Add(new OperationOutcome.IssueComponent()
                    //{
                    //    Severity = OperationOutcome.IssueSeverity.Error,
                    //    Code = OperationOutcome.IssueType.Exception,
                    //    Diagnostics = ex.Message
                    //});
                    //result.Entry.Add(new Bundle.EntryComponent()
                    //{
                    //    Search = new Bundle.SearchComponent() { Mode = Bundle.SearchEntryMode.Outcome },
                    //    Resource = oe
                    //});
                }
            }

            return System.Threading.Tasks.Task.FromResult(con);
        }

        public IFhirResourceServiceSTU3 GetResourceService(ModelBaseInputs request, string resourceName)
        {
            if (string.Compare(resourceName, "endpoint", true) == 0)
            {
                if (request.RequestUri.Query.Contains("administer-federation"))
                {
                    return new DirectoryResourceService() { RequestDetails = request, ResourceName = resourceName };
                }
            }
            return new FederatedResourceService(Members()) { RequestDetails = request, ResourceName = resourceName };
        }

        public Task<Resource> PerformOperation(ModelBaseInputs request, string operation, Parameters operationParameters, SummaryType summary)
        {
            throw new NotImplementedException();
        }

        public Task<Bundle> ProcessBatch(ModelBaseInputs request, Bundle bundle)
        {
            throw new NotImplementedException();
        }

        public Task<Bundle> Search(ModelBaseInputs request, IEnumerable<KeyValuePair<string, string>> parameters, int? Count, SummaryType summary)
        {
            throw new NotImplementedException();
        }

        public Task<Bundle> SystemHistory(ModelBaseInputs request, DateTimeOffset? since, DateTimeOffset? Till, int? Count, SummaryType summary)
        {
            Bundle result = new Bundle();
            result.Meta = new Meta();
            result.Meta.LastUpdated = DateTime.Now;
            result.Id = new Uri("urn:uuid:" + Guid.NewGuid().ToString("n")).OriginalString;
            result.Type = Bundle.BundleType.History;

            var parser = new Fhir.Serialization.FhirXmlParser();
            var files = System.IO.Directory.EnumerateFiles(DirectorySystemService.Directory);
            foreach (var filename in files)
            {
                var resource = parser.Parse<Resource>(System.IO.File.ReadAllText(filename));
                result.AddResourceEntry(resource,
                    ResourceIdentity.Build(request.BaseUri,
                        resource.ResourceType.ToString(),
                        resource.Id,
                        resource.Meta.VersionId).OriginalString);
            }
            result.Total = result.Entry.Count;

            // also need to set the page links

            return System.Threading.Tasks.Task.FromResult(result);
        }
    }
}
