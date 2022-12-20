//
// Very Simple Graph for serializing to a DGML file format
//

using System.Collections.Generic;
using System.Xml;

namespace Walkabout.Utilities
{
    class SimpleGraph
    {
        public Dictionary<string, SimpleGraphNode> Nodes;
        public List<SimpleGraphLink> Links;

        public SimpleGraph()
        {
            this.Nodes = new Dictionary<string, SimpleGraphNode>();
            this.Links = new List<SimpleGraphLink>();
        }

        public SimpleGraphNode AddOrGetNode(string Id)
        {
            SimpleGraphNode node;

            if (this.Nodes.TryGetValue(Id, out node) == false)
            {
                node = new SimpleGraphNode(Id);
                this.Nodes.Add(Id, node);
            }

            return node;
        }

        public SimpleGraphLink GetOrAddLink(string source, string target)
        {
            SimpleGraphNode nodeSource = this.AddOrGetNode(source);
            SimpleGraphNode nodeTarget = this.AddOrGetNode(target);

            SimpleGraphLink link = new SimpleGraphLink(nodeSource, nodeTarget);

            int index = nodeSource.LinkTarget.IndexOf(nodeTarget);

            if (index == -1)
            {
                nodeSource.LinkTarget.Add(nodeTarget);

                // Also update the global Links on the Graph
                this.Links.Add(link);
            }
            else
            {
                // This link already exist
                foreach (SimpleGraphLink l in this.Links)
                {
                    if (l.Source == nodeSource && l.Target == nodeTarget)
                    {
                        return l;
                    }
                }
            }


            return link;
        }

        /// <summary>
        /// Save in the DGML format
        /// </summary>
        /// <param name="file"></param>
        public void Save(string file, string optionalStyles)
        {
            XmlDocument doc = this.ToXml(optionalStyles);
            doc.Save(file);
        }

        public XmlDocument ToXml(string optionalStyles)
        {
            XmlDocument doc = new XmlDocument();
            doc.InnerXml = "<DirectedGraph xmlns=\"http://schemas.microsoft.com/vs/2009/dgml\">"
                            + optionalStyles +
                            "</DirectedGraph>";

            string ns = "http://schemas.microsoft.com/vs/2009/dgml";

            XmlNode rootNodes = doc.CreateElement("Nodes", ns);
            doc.DocumentElement.AppendChild(rootNodes);


            foreach (SimpleGraphNode sgn in this.Nodes.Values)
            {
                XmlElement n = doc.CreateElement("Node", ns);
                n.SetAttribute("Id", sgn.Id);
                if (sgn.Label != null)
                {
                    n.SetAttribute("Label", sgn.Label);
                }
                n.SetAttribute("Category", sgn.Category);

                rootNodes.AppendChild(n);
            }


            XmlNode rootLinks = doc.CreateElement("Links", ns);
            doc.DocumentElement.AppendChild(rootLinks);

            foreach (SimpleGraphLink sgl in this.Links)
            {
                XmlElement l = doc.CreateElement("Link", ns);
                l.SetAttribute("Source", sgl.Source.Id);
                l.SetAttribute("Target", sgl.Target.Id);
                l.SetAttribute("Category", sgl.Category);


                foreach (SimpleGraphProperty sgp in sgl.Properties)
                {
                    l.SetAttribute(sgp.Id, sgp.Value.ToString());
                }
                rootLinks.AppendChild(l);
            }
            return doc;
        }
    }


    class SimpleGraphNode : SimpleGraphEntry
    {
        public string Id;
        public List<SimpleGraphNode> LinkTarget;

        public SimpleGraphNode(string Id)
        {
            this.Id = Id;
            this.LinkTarget = new List<SimpleGraphNode>();
        }

        public string Label { get; set; }
    }

    class SimpleGraphLink : SimpleGraphEntry
    {
        public SimpleGraphNode Source;
        public SimpleGraphNode Target;

        public SimpleGraphLink(SimpleGraphNode source, SimpleGraphNode target)
        {
            this.Source = source;
            this.Target = target;
        }
    }

    class SimpleGraphEntry
    {
        public List<SimpleGraphProperty> Properties = new List<SimpleGraphProperty>();
        public string Category;

        public SimpleGraphProperty AddProperty(string id, object value)
        {
            SimpleGraphProperty sgp = new SimpleGraphProperty();
            sgp.Id = id;
            sgp.Value = value;
            this.Properties.Add(sgp);
            return sgp;
        }

        public SimpleGraphProperty GetProperty(string id)
        {
            foreach (SimpleGraphProperty sgp in this.Properties)
            {
                if (sgp.Id == id)
                {
                    return sgp;
                }
            }
            return null;
        }

    }


    class SimpleGraphProperty
    {
        public string Id;
        public object Value;
    }
}
