﻿using System;
using System.Collections.Generic;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.WebApi;
using Hl7.Fhir.Utility;

namespace Hl7.DemoFileSystemFhirServer
{
    public class DirectoryResourceService : Hl7.Fhir.WebApi.IFhirResourceServiceSTU3
    {
        public ModelBaseInputs RequestDetails { get; set; }

        public string ResourceName { get; set; }

        public System.Threading.Tasks.Task<Resource> Create(Resource resource, string ifMatch, string ifNoneExist, DateTimeOffset? ifModifiedSince)
        {
            if (String.IsNullOrEmpty(resource.Id))
                resource.Id = Guid.NewGuid().ToFhirId();
            if (resource.Meta == null)
                resource.Meta = new Meta();
            resource.Meta.LastUpdated = DateTime.Now;
            string path = System.IO.Path.Combine(DirectorySystemService.Directory, $"{resource.TypeName}.{resource.Id}.{resource.Meta.VersionId}.xml");
            System.IO.File.WriteAllText(
                path,
                Hl7.Fhir.Serialization.FhirSerializer.SerializeResourceToXml(resource));
            resource.SetAnnotation<CreateOrUpate>(CreateOrUpate.Create);
            return System.Threading.Tasks.Task.FromResult(resource);
        }

        public System.Threading.Tasks.Task<string> Delete(string id, string ifMatch)
        {
            string path = System.IO.Path.Combine(DirectorySystemService.Directory, $"{this.ResourceName}.{id}..xml");
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
            return System.Threading.Tasks.Task.FromResult<string>(null);
        }

        public System.Threading.Tasks.Task<Resource> Get(string resourceId, string VersionId, SummaryType summary)
        {
            string path = System.IO.Path.Combine(DirectorySystemService.Directory, $"{this.ResourceName}.{resourceId}.{VersionId}.xml");
            if (System.IO.File.Exists(path))
                return System.Threading.Tasks.Task.FromResult<Resource>( new Fhir.Serialization.FhirXmlParser().Parse<Resource>(System.IO.File.ReadAllText(path)));
            return System.Threading.Tasks.Task.FromResult<Resource>(null);
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
            result.Meta = new Meta()
            {
                LastUpdated = DateTime.Now
            };
            result.Id = new Uri("urn:uuid:" + Guid.NewGuid().ToString("n")).OriginalString;
            result.Type = Bundle.BundleType.Searchset;

            var parser = new Fhir.Serialization.FhirXmlParser();
            var files = System.IO.Directory.EnumerateFiles(DirectorySystemService.Directory, $"{ResourceName}.*.xml");
            foreach (var filename in files)
            {
                // TODO: actually filter!
                var resource = parser.Parse<Resource>(System.IO.File.ReadAllText(filename));
                result.AddResourceEntry(resource,
                    ResourceIdentity.Build(RequestDetails.BaseUri,
                        resource.ResourceType.ToString(),
                        resource.Id,
                        resource.Meta.VersionId).OriginalString);
            }
            result.Total = result.Entry.Count;

            // also need to set the page links

            return System.Threading.Tasks.Task.FromResult(result);
        }

        public System.Threading.Tasks.Task<Bundle> TypeHistory(DateTimeOffset? since, DateTimeOffset? Till, int? Count, SummaryType summary)
        {
            Bundle result = new Bundle();
            result.Meta = new Meta()
            {
                LastUpdated = DateTime.Now
            };
            result.Id = new Uri("urn:uuid:" + Guid.NewGuid().ToString("n")).OriginalString;
            result.Type = Bundle.BundleType.History;

            var parser = new Fhir.Serialization.FhirXmlParser();
            var files = System.IO.Directory.EnumerateFiles(DirectorySystemService.Directory, $"{ResourceName}.*.xml");
            foreach (var filename in files)
            {
                var resource = parser.Parse<Resource>(System.IO.File.ReadAllText(filename));
                result.AddResourceEntry(resource,
                    ResourceIdentity.Build(RequestDetails.BaseUri,
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
