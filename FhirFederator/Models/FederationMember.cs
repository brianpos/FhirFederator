using FhirFederator.Utils;
using Hl7.Fhir.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Hl7.Fhir.Rest;

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
        }
        public string Name;
        public string Url;
        public string IdentifierNamespace;
        public Hl7.Fhir.Rest.ResourceFormat Format;
        public string[] Headers;
        public X509Certificate2 Certificate;


        public Provenance CreateProvenance()
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
            return prov;
        }

        public Hl7.Fhir.Model.Provenance WithProvenance(Provenance prov, Resource resource, string fullUrl)
        {
            // create the resource reference (with the full URL intact)
            ResourceReference resRef = null;
            ResourceReference relativeRef = null;
            if (!string.IsNullOrEmpty(resource.Id))
            {
                resRef = new ResourceReference(resource.ResourceIdentity(resource.ResourceBase).OriginalString);
                relativeRef = new ResourceReference(ResourceIdentity.Build(resource.TypeName, resource.Id, resource.Meta?.VersionId).OriginalString);

                // Append the display (like all good servers should)
                if (resource is Organization org)
                {
                    resRef.Display = org.Name;
                }
                if (resource is Location loc)
                {
                    resRef.Display = loc.Name;
                }
                if (resource is Endpoint ep)
                {
                    resRef.Display = ep.Name;
                }
                if (resource is HealthcareService hcs)
                {
                    resRef.Display = hcs.Name;
                }
                if (resource is Practitioner prac)
                {
                    resRef.Display = prac.Name.FirstOrDefault()?.Text;
                }
                if (resource is PractitionerRole pracRole)
                {
                    resRef.Display = pracRole.Practitioner.Display + " - " + string.Join(", ", pracRole.Code?.FirstOrDefault()?.Coding?.FirstOrDefault()?.Display);
                }
                relativeRef.Display = resRef.Display;
            }
            // include the resource in the provenance
            Element what = resRef;
            if (resRef != null)
            {
                prov.Target.Add(relativeRef);
            }
            else
            {
                what = new FhirUri(fullUrl);
            }
            prov.Entity.Add(new Provenance.EntityComponent()
            {
                Role = Provenance.ProvenanceEntityRole.Source,
                // this is the URL of the original source content
                What = what
            });
            return prov;
        }

        public void PrepareFhirClientSecurity(FhirClient server)
        {
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
    }
}
