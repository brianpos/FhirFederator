using Hl7.Fhir.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FhirFederator.Models
{
    public class FederationMember
    {
        public FederationMember(Endpoint ep)
        {
            Name = ep.Name;
            Url = ep.Address;
        }
        public string Name;
        public string Url;
        public string IdentifierNamespace;

        public Provenance CreateProvenance()
        {
            var prov = new Provenance();
            prov.Recorded = DateTimeOffset.Now;
            return prov;
        }

        public Hl7.Fhir.Model.Provenance WithProvenance(Provenance prov, Resource resource)
        {
            // create the resource reference (with the full URL intact)
            var resRef = new ResourceReference(resource.ResourceIdentity(resource.ResourceBase).OriginalString);

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

            // include the resource in the provenance
            prov.Target.Add(resRef);
            return prov;
        }
    }
}
