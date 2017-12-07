using FhirFederator.Utils;
using Hl7.Fhir.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Hl7.Fhir.Rest;
using Hl7.Fhir.FhirPath;

namespace FhirFederator.Models
{
    public class FederationMember
    {
        public FederationMember(Endpoint ep)
        {
            Name = ep.Name;
            Url = ep.Address;
            if (ep.PayloadMimeType.Contains("application/fhir+xml"))
                Format = Hl7.Fhir.Rest.ResourceFormat.Xml;
            else if (ep.PayloadMimeType.Contains("application/fhir+json"))
                Format = Hl7.Fhir.Rest.ResourceFormat.Json;
            else
                Format = Hl7.Fhir.Rest.ResourceFormat.Xml;
            Headers = ep.Header.ToArray();
            string thubmprint = ep.GetStringExtension("http://standards.telstrahealth.com.au/fhir/federation-thumbprint");
            if (!string.IsNullOrEmpty(thubmprint))
                Certificate = CertificateHelper.FindCertificateByThumbprint(thubmprint);
            IdPrefix = ep.GetStringExtension("http://standards.telstrahealth.com.au/fhir/federation-id-prefix");
            if (string.IsNullOrEmpty(IdPrefix))
                IdPrefix = ep.Id.Substring(0, 2) + "-";
        }
        public string Name;
        public string Url;
        public string IdentifierNamespace;
        public string IdPrefix;
        public Hl7.Fhir.Rest.ResourceFormat Format;
        public string[] Headers;
        public X509Certificate2 Certificate;


        public Hl7.Fhir.Model.Provenance CreateProvenance(Resource resource, string fullUrl)
        {
            var prov = new Provenance();
            prov.Recorded = DateTimeOffset.Now;
            prov.Agent.Add(new Provenance.AgentComponent()
            {
                Role = new List<CodeableConcept>() { new CodeableConcept("http://hl7.org/fhir/v3/ParticipationType", "CST", "custodian", "Custodian") },
                Who = new ResourceReference()
                {
                    Reference = Url,
                    Display = Name
                }
            });

            // create the resource reference (with the full URL intact)
            ResourceReference reference = new ResourceReference();
            // Append the display (like all good servers should)
            if (resource is Organization org)
            {
                reference.Display = org.Name;
            }
            if (resource is Location loc)
            {
                reference.Display = loc.Name;
            }
            if (resource is Endpoint ep)
            {
                reference.Display = ep.Name;
            }
            if (resource is HealthcareService hcs)
            {
                reference.Display = hcs.Name;
            }
            if (resource is Practitioner prac)
            {
                reference.Display = prac.Name.FirstOrDefault()?.Text;
            }
            if (resource is PractitionerRole pracRole)
            {
                reference.Display = pracRole.Practitioner.Display + " - " + string.Join(", ", pracRole.Code?.FirstOrDefault()?.Coding?.FirstOrDefault()?.Display);
            }

            ResourceIdentity ri = new ResourceIdentity(resource.Meta.GetExtensionValue<FhirUri>("http://hl7.org/fhir/StructureDefinition/extension-Meta.source|3.2").Value);
            reference.Reference = ri.OriginalString;
            prov.Entity.Add(new Provenance.EntityComponent()
            {
                Role = Provenance.ProvenanceEntityRole.Source,
                // this is the URL of the original source content
                What = reference
            });

            ResourceReference relativeRef = (ResourceReference)reference.DeepCopy();
            if (ri.Form == ResourceIdentityForm.AbsoluteRestUrl || ri.Form == ResourceIdentityForm.RelativeRestUrl)
            {
                relativeRef.Reference = ResourceIdentity.Build(ri.ResourceType, IdPrefix + ri.Id, ri.VersionId).OriginalString;
            }
            prov.Target.Add(relativeRef);
            return prov;
        }

        public void PrepareFhirClient(FhirClient server)
        {
            // Set the connection preferences
            server.PreferCompressedResponses = true;
            server.PreferredFormat = Format;

            // Setup the security details
            if (Headers?.Length > 0 || Certificate != null)
            {
                server.OnBeforeRequest += (object sender, BeforeRequestEventArgs e) =>
                {
                    if (Headers?.Length > 0)
                    {
                        foreach (var item in Headers)
                        {
                            if (item.Contains(":"))
                            {
                                string key = item.Substring(0, item.IndexOf(":"));
                                string value = item.Substring(item.IndexOf(":") + 1).Trim();
                                e.RawRequest.Headers[key] = value;
                            }
                        }
                    }
                    if (Certificate != null)
                    {
                        e.RawRequest.ClientCertificates.Add(Certificate);
                    }
                };
            }
        }

        /// <summary>
        /// This will update all the ResourceReferences (where are relative, or absolute to this server, as relative with a prefix)
        /// It will also process any URIs in the same way
        /// </summary>
        /// <param name="resource"></param>
        public void RewriteIdentifiers(Resource resource, Uri federatorBaseUri, string directUri)
        {
            if (resource.Meta == null)
                resource.Meta = new Meta();
            FhirUri sourceUri = new FhirUri(directUri);
            if (!string.IsNullOrEmpty(resource.Id))
                sourceUri = new FhirUri(resource.ResourceIdentity(resource.ResourceBase).OriginalString);

            // and Identifiers will need to be adjusted to remove any
            // absolute references that are to the federation member itself
            // and also prefix any Ids with the members ID
            resource.Id = IdPrefix + resource.Id;

            foreach (Element elem in resource.Select("descendants().where($this.as(Reference) or $this.as(uri))"))
            {
                if (elem is ResourceReference resRef)
                {
                    // Clean the Identifier
                    RewriteResourceReference(resRef, federatorBaseUri);
                }
                if (elem is FhirUri uri)
                {
                    // Clean the URI
                    RewriteFhirUri(uri, federatorBaseUri);
                }
            }

            // now that all the conversions have been completed, put in the source (so it doesn't get re-written)
            resource.Meta.AddExtension("http://hl7.org/fhir/StructureDefinition/extension-Meta.source|3.2", sourceUri);
        }

        public string RewriteFhirUri(FhirUri uri, Uri federatorBaseUri)
        {
            if (!string.IsNullOrEmpty(uri.Value))
            {
                ResourceIdentity ri = new ResourceIdentity(uri.Value);
                if ((ri.Form == ResourceIdentityForm.AbsoluteRestUrl)
                    && ri.BaseUri.OriginalString.TrimEnd('/').ToLower() == this.Url.ToLower())
                {
                    uri.Value = ResourceIdentity.Build(federatorBaseUri, ri.ResourceType, IdPrefix + ri.Id, ri.VersionId).OriginalString;
                }
                if (ri.Form == ResourceIdentityForm.RelativeRestUrl)
                {
                    uri.Value = ResourceIdentity.Build(ri.ResourceType, IdPrefix + ri.Id, ri.VersionId).OriginalString;
                }
            }
            return uri?.Value;
        }

        public void RewriteResourceReference(ResourceReference resRef, Uri federatorBaseUri)
        {
            if (!string.IsNullOrEmpty(resRef.Reference) && !resRef.Reference.StartsWith("#"))
            {
                ResourceIdentity ri = new ResourceIdentity(resRef.Reference);
                if (ri.Form == ResourceIdentityForm.AbsoluteRestUrl && ri.BaseUri.OriginalString.TrimEnd('/').ToLower() == this.Url.ToLower())
                {
                    resRef.Reference = ResourceIdentity.Build(federatorBaseUri, ri.ResourceType, IdPrefix + ri.Id, ri.VersionId).OriginalString;
                }
                if (ri.Form == ResourceIdentityForm.RelativeRestUrl)
                {
                    resRef.Reference = ResourceIdentity.Build(ri.ResourceType, IdPrefix + ri.Id, ri.VersionId).OriginalString;
                }
            }
        }
    }
}
