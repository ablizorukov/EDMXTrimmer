using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Linq;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace EDMXTrimmer
{
    class EdmxTrimmer
    {
        public string EdmxFile { get; private set; }
        public bool Verbose { get; private set; }
        public List<string> EntitiesToKeep { get; private set; }
        public List<string> EntitiesToExclude { get; private set; }
        public string OutputFileName { get; set; }

        private XmlDocument _xmlDocument;
        private const string ENTITY_TYPE = "EntityType";
        private const string ENTITY_SET = "EntitySet";
        private const string NAVIGATION_PROPERTY = "NavigationProperty";
        private const string ACTION = "Action";
        private const string ATTRIBUTE_NAME = "Name";
        private const string ATTRIBUTE_TYPE = "Type";
        private const string ENTITYNAMESPACE = "Microsoft.Dynamics.DataEntities.";

        public EdmxTrimmer(
            string edmxFile, 
            string outputFileName,  
            bool verbose = true, 
            List<String> entitiesToKeep = null,
            List<String> entitiesToExclude = null)
        {
            this.EdmxFile = edmxFile;
            this.Verbose = verbose;
            this.OutputFileName = outputFileName;

            this.EntitiesToKeep = new List<string>();
            if(entitiesToKeep != null && entitiesToKeep.Count > 0)
            {
                this.EntitiesToKeep.AddRange(entitiesToKeep);
            }

            this.EntitiesToExclude = new List<string>();
            if (entitiesToExclude != null && entitiesToExclude.Count > 0)
            {
                this.EntitiesToExclude.AddRange(entitiesToExclude);
            }

            this.LoadFile();
        }

        private void LoadFile()
        {
            this._xmlDocument = new XmlDocument();
            this._xmlDocument.Load(this.EdmxFile);
        }

        public void AnalyzeFile()
        {
            var entitySets = this._xmlDocument.GetElementsByTagName(ENTITY_SET).Cast<XmlNode>().ToList();
            var entityTypes = this._xmlDocument.GetElementsByTagName(ENTITY_TYPE).Cast<XmlNode>().ToList();

            //if (this.Verbose)
            //{
            //    // Print list of ALL entities
            //    entitySets.ForEach(n => Console.WriteLine(n.Attributes[ATTRIBUTE_NAME].Value));
            //}

            if (this.EntitiesToKeep.Count > 0)
            {
                RemoveAllEntitiesExcept(this.EntitiesToKeep, entitySets, entityTypes);
            }

            if (this.EntitiesToExclude.Count > 0)
            {
                RemoveExcludedEntities(this.EntitiesToExclude, entitySets, entityTypes);
            }

            this._xmlDocument.Save(OutputFileName);
            if(this.Verbose)
            {
                Console.WriteLine($"EDMX Saved to file: {OutputFileName}");
            }

        }

        private void RemoveAllEntitiesExcept(
            List<string> entitiesToKeep, 
            List<XmlNode> entitySets, 
            List<XmlNode> entityTypes)
        {
            var entitiesKeep = entitySets.Where(n => entitiesToKeep.Contains(n.Attributes[ATTRIBUTE_NAME].Value)).ToList();

            RemoveEntitySets(entitySets, entitiesKeep);
            RemoveEntityTypes(entityTypes, entitySets, entitiesKeep);
        }

        private void RemoveExcludedEntities(
            List<string> entitiesToExclude, 
            List<XmlNode> entitySets, 
            List<XmlNode> entityTypes)
        {
            var entitiesKeep = entitySets.Where(n => !entitiesToExclude.Contains(n.Attributes[ATTRIBUTE_NAME].Value)).ToList();

            RemoveEntitySets(entitySets, entitiesKeep);
            RemoveEntityTypes(entityTypes, entitySets, entitiesKeep);
        }

        private void RemoveEntitySets(List<XmlNode> entitySets, List<XmlNode> entitiesKeep)
        {
            // Remove entities not required (EntitySet)
            entitySets.Except(entitiesKeep).ToList().ForEach(n => n.ParentNode.RemoveChild(n));
            //Remove unwanted Nodes in the Entity Set
            entitiesKeep.ForEach(n =>
            {
                // Remove Node NavigationProperty
                var navProperties = n.ChildNodes.Cast<XmlNode>()
                    .Where(navProp => navProp.Name.Equals(NAVIGATION_PROPERTY)).ToList();
                navProperties
                    .ForEach(navProp => navProp.ParentNode.RemoveChild(navProp));
            });
        }

        private void RemoveEntityTypes(List<XmlNode> entityTypes, List<XmlNode> entitySets, List<XmlNode> entitiesKeep)
        {
            List<String> entityTypesFound = new List<string>();
            entitiesKeep.ForEach(n =>
            {
                string entityType = n.Attributes[ENTITY_TYPE].Value;
                entityType = entityType.Replace(ENTITYNAMESPACE, "");
                entityTypesFound.Add(entityType);
            });

            // Remove all navigation properties
            this._xmlDocument.GetElementsByTagName(NAVIGATION_PROPERTY).Cast<XmlNode>()
                .Where(navProp => !entityTypesFound.Any(entityType => EntityExists(navProp, entityType))).ToList()
                .ForEach(n => n.ParentNode.RemoveChild(n));

            // Remove entity not required (EntityType)
            var entityTypesKeep = entityTypes.Where(n => entityTypesFound.Contains(n.Attributes[ATTRIBUTE_NAME].Value)).ToList();
            entityTypes.Except(entityTypesKeep).ToList().ForEach(n => n.ParentNode.RemoveChild(n));

            // Remove all Actions         
            this._xmlDocument.GetElementsByTagName(ACTION).Cast<XmlNode>()
                .Where(action => !entityTypesFound.Any(entityType => action.ChildNodes.Cast<XmlNode>().
                    Any(childNode => EntityExists(childNode, entityType)))).ToList()
                .ForEach(n => n.ParentNode.RemoveChild(n));
        }

        private bool EntityExists(XmlNode xmlNode, string entityType)
        {
            return xmlNode.Attributes[ATTRIBUTE_TYPE] == null ? false : Regex.IsMatch(xmlNode.Attributes[ATTRIBUTE_TYPE].Value, ENTITYNAMESPACE + entityType + "\\)?$");
        }
    }
}
