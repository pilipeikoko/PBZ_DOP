using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AngleSharp.Common;
using VDS.RDF;
using VDS.RDF.Ontology;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using static System.String;

namespace OwlGenerator
{
    public class OntologyService
    {
        private static string DefaultLink = "http://my.valeksdelal.meeew/";

        public static void Main()
        {
        }

        public OntologyClass CreateClass(OntologyGraph graph, string name)
        {
            var generatedClass
                = graph.CreateOntologyClass(UriFactory.Create($"{DefaultLink}/{name}"));

            generatedClass.AddType(UriFactory.Create(OntologyHelper.OwlClass));
            generatedClass.AddLabel(name, "en");

            return generatedClass;
        }

        public IUriNode Create(OntologyGraph graph, string name)
        {
            var generatedClass
                = graph.CreateUriNode(UriFactory.Create($"{DefaultLink}/{name}"));

            return generatedClass;
        }

        public OntologyClass CreateSubClass(OntologyGraph graph, string superClassName, string name)
        {
            var generatedClass
                = graph.CreateOntologyClass(UriFactory.Create($"{FindNode(graph, superClassName)}/{name}"));

            generatedClass.AddType(UriFactory.Create(OntologyHelper.OwlClass));
            generatedClass.AddLabel(name, "en");
            generatedClass.AddSuperClass(FindNode(graph, superClassName));

            return generatedClass;
        }

        public Individual CreateIndividual(OntologyGraph graph, string className, string name)
        {
            var generatedClass =
                graph.CreateIndividual(Create(graph, className + "/" + name), FindUriNode(graph, className));

            generatedClass.AddLabel(name, "en");

            return generatedClass;
        }

        private Uri FindNode(OntologyGraph graph, string name)
        {
            var node = graph.Nodes.Where(x => x.NodeType == NodeType.Uri).Select(x => (UriNode) x).ToList();

            return node.FirstOrDefault(x => x.Uri.ToString().Equals(DefaultLink + "/" + name))?.Uri;
        }

        private UriNode FindUriNode(OntologyGraph graph, string name)
        {
            var node = graph.Nodes.Where(x => x.NodeType == NodeType.Uri).Select(x => (UriNode) x).ToList();

            return node.FirstOrDefault(x => x.Uri.ToString().Equals(DefaultLink + "/" + name));
        }

        public void DeleteClass(OntologyGraph graph, string nodeFullPath)
        {
            var triplesWhereObject = graph.Triples
                .Where(x => x.Object.NodeType == NodeType.Uri)
                .Select(x => (x, (UriNode) x.Object))
                .Where(y => y.Item2.Uri.AbsoluteUri.Contains(nodeFullPath))
                .Select(x => x.x)
                .ToList();

            var triplesWhereSubject = graph.Triples
                .Where(x => x.Subject.NodeType == NodeType.Uri)
                .Select(x => (x, (UriNode) x.Subject))
                .Where(y => y.Item2.Uri.AbsoluteUri.Contains(nodeFullPath))
                .Select(x => x.x)
                .ToList();


            graph.Retract(triplesWhereObject);
            graph.Retract(triplesWhereSubject);
        }

        public void UpdateUriNode(OntologyGraph graph, string parent, string name, string oldName)
        {
            CreateSubClass(graph, parent, name);
            UpdateChildren(graph, parent, name, oldName);
        }

        private void UpdateChildren(OntologyGraph graph, string parent, string name, string oldName)
        {
            var nodes = graph.Nodes
                .Where(x => x.NodeType == NodeType.Uri)
                .Select(x => (UriNode) x)
                .Where(x => x.Uri.AbsoluteUri.Contains(FindNode(graph, parent).AbsoluteUri + "/" + oldName))
                .ToList();

            foreach (var node in nodes)
            {
                DeleteClass(graph, node.Uri.AbsoluteUri);
            }

            nodes = graph.Nodes
                .Where(x => x.NodeType == NodeType.Uri)
                .Select(x => (UriNode) x)
                .Where(x => x.Uri.AbsoluteUri.Contains(FindNode(graph, parent).AbsoluteUri + "/" + oldName))
                .ToList();

            foreach (var node in nodes)
            {
                CreateClass(graph, node.Uri.AbsoluteUri.Replace(oldName, name));
            }
        }

        public List<string> GetNodes(OntologyGraph graph)
        {
            var nodes = graph.Nodes
                .Where(x => x.NodeType == NodeType.Uri)
                .Select(x => (UriNode) x)
                .Select(x => x.Uri.AbsoluteUri.Replace(DefaultLink, Empty))
                .Where(x => graph.Triples
                    .Any(y => (y.Object.NodeType == NodeType.Uri && ((UriNode) y.Object).Uri.AbsoluteUri.Contains(x))
                              || (y.Subject.NodeType == NodeType.Uri &&
                                  ((UriNode) y.Subject).Uri.AbsoluteUri.Contains(x))))
                .ToList();


            return nodes.OrderBy(x => x).ToList();
        }

        public List<string> GetAllNodesWitHPrettyName(OntologyGraph graph)
        {
            var nodes = graph.Nodes
                .Where(x => x.NodeType == NodeType.Uri)
                .Select(x => (UriNode) x)
                .Where(x => x.Uri.ToString().Contains(DefaultLink))
                .Select(x => x.Uri.AbsoluteUri.Replace(DefaultLink, Empty))
                .Where(x => graph.Triples
                    .Any(y => (y.Object.NodeType == NodeType.Uri && ((UriNode) y.Object).Uri.AbsoluteUri.Contains(x))
                              || (y.Subject.NodeType == NodeType.Uri &&
                                  ((UriNode) y.Subject).Uri.AbsoluteUri.Contains(x))))
                .ToList();


            return nodes.OrderBy(x => x).ToList();
        }

        public void AddToScMemory(OntologyGraph graph)
        {
            var allNodes = graph.Triples
                .Where(x => x.Predicate.NodeType == NodeType.Uri)
                .Select(x => ((UriNode) x.Predicate, x.Subject))
                .Where(x => x.Item1.Uri.ToString() == "http://www.w3.org/1999/02/22-rdf-syntax-ns#type")
                .Select(x => (UriNode) x.Subject)
                .Select(x =>
                    x.Uri.ToString().Substring(x.Uri.ToString().LastIndexOf("/", StringComparison.Ordinal) + 1))
                .ToList();

            var classes = graph.OwlClasses
                .Select(x => x?.Label?.FirstOrDefault())
                .Select(x => new
                {
                    Value = x?.Value,
                    Language = x?.Language
                })
                .ToList();

            var individuals = allNodes
                .Where(x => classes
                    .FirstOrDefault(y => y.Value == x) == null)
                .ToList();

            var individualsWithParent = individuals
                .Select(x => (Individual: x, Parent: GetIndividualParent(graph, x)))
                .ToList();

            var classesWithParent = classes
                .Select(x => (Class: x.Value, Language: x.Language, Parent: GetClassParent(graph, x.Value)))
                .ToList();

            List<(string Object, string RelationName, string Subject)> customTriples = graph.Triples.Where(x => x.Predicate.NodeType == NodeType.Uri)
                .Select(x => (x, (IUriNode) x.Predicate))
                .Where(x => x.Item2.Uri.ToString().Contains(DefaultLink))
                .Select(x => (Object: x.x.Object, Predicate: x.x.Predicate, Subject: x.x.Subject))
                .Where(x => x.Object.NodeType == NodeType.Uri && x.Subject.NodeType == NodeType.Uri &&
                            x.Predicate.NodeType == NodeType.Uri)
                .Select(x => (Object: (IUriNode) x.Object, Subject: (IUriNode) x.Subject,
                    Predicate: (IUriNode) x.Predicate))
                .Select(x =>
                    (Object: x.Object.Uri.ToString().Substring(x.Object.Uri.ToString().LastIndexOf("/", StringComparison.Ordinal) + 1),
                        Predicate: x.Predicate.Uri.ToString()
                            .Substring(x.Predicate.Uri.ToString().LastIndexOf("/", StringComparison.Ordinal) + 1),
                        Subject: x.Subject.Uri.ToString()
                            .Substring(x.Subject.Uri.ToString().LastIndexOf("/", StringComparison.Ordinal) + 1)))
                .ToList();

            SemanticService service = new();

            service.AddToScMemory(classesWithParent, individualsWithParent, customTriples);
        }
        
        public string ConvertToScs(OntologyGraph graph)
        {
            var allNodes = graph.Triples
                .Where(x => x.Predicate.NodeType == NodeType.Uri)
                .Select(x => ((UriNode) x.Predicate, x.Subject))
                .Where(x => x.Item1.Uri.ToString() == "http://www.w3.org/1999/02/22-rdf-syntax-ns#type")
                .Select(x => (UriNode) x.Subject)
                .Select(x =>
                    x.Uri.ToString().Substring(x.Uri.ToString().LastIndexOf("/", StringComparison.Ordinal) + 1))
                .ToList();

            var classes = graph.OwlClasses
                .Select(x => x?.Label?.FirstOrDefault())
                .Select(x => new
                {
                    Value = x?.Value,
                    Language = x?.Language
                })
                .ToList();

            var individuals = allNodes
                .Where(x => classes
                    .FirstOrDefault(y => y.Value == x) == null)
                .ToList();

            var individualsWithParent = individuals
                .Select(x => (Individual: x, Parent: GetIndividualParent(graph, x)))
                .ToList();

            var classesWithParent = classes
                .Select(x => (Class: x.Value, Language: x.Language, Parent: GetClassParent(graph, x.Value)))
                .ToList();

            List<(string Object, string RelationName, string Subject)> customTriples = graph.Triples.Where(x => x.Predicate.NodeType == NodeType.Uri)
                .Select(x => (x, (IUriNode) x.Predicate))
                .Where(x => x.Item2.Uri.ToString().Contains(DefaultLink))
                .Select(x => (Object: x.x.Object, Predicate: x.x.Predicate, Subject: x.x.Subject))
                .Where(x => x.Object.NodeType == NodeType.Uri && x.Subject.NodeType == NodeType.Uri &&
                            x.Predicate.NodeType == NodeType.Uri)
                .Select(x => (Object: (IUriNode) x.Object, Subject: (IUriNode) x.Subject,
                    Predicate: (IUriNode) x.Predicate))
                .Select(x =>
                    (Object: x.Object.Uri.ToString().Substring(x.Object.Uri.ToString().LastIndexOf("/", StringComparison.Ordinal) + 1),
                        Predicate: x.Predicate.Uri.ToString()
                            .Substring(x.Predicate.Uri.ToString().LastIndexOf("/", StringComparison.Ordinal) + 1),
                        Subject: x.Subject.Uri.ToString()
                            .Substring(x.Subject.Uri.ToString().LastIndexOf("/", StringComparison.Ordinal) + 1)))
                .ToList();

            ScsService service = new();

            return service.ConvertToScs(classesWithParent, individualsWithParent, customTriples);
        }

        private string GetIndividualParent(OntologyGraph graph, string value)
        {
            var first = graph.Triples
                .Where(x => (x.Subject.NodeType == NodeType.Uri && x.Object.NodeType == NodeType.Uri))
                .Select(x => ((UriNode) x.Object, (UriNode) x.Subject))
                .Where(x => x.Item2.Uri.ToString().Contains(value))
                .Select(x => x.Item1)
                .ToList()
                .FirstOrDefault();

            return first?.Uri.ToString()[(first.Uri.ToString().LastIndexOf("/", StringComparison.Ordinal) + 1)..];
        }

        private string GetClassParent(OntologyGraph graph, string value)
        {
            if (value == "Thing")
            {
                return null;
            }

            var first = graph.Triples
                .Where(x => (x.Subject.NodeType == NodeType.Uri && x.Object.NodeType == NodeType.Uri &&
                             x.Predicate.NodeType == NodeType.Uri))
                .Select(x => ((UriNode) x.Object, (UriNode) x.Subject, (UriNode) x.Predicate))
                .Where(x => (x.Item2.Uri.ToString().Contains(value) &&
                             x.Item3.Uri.ToString() == "http://www.w3.org/2000/01/rdf-schema#subClassOf"))
                .Select(x => x.Item1)
                .ToList()
                .FirstOrDefault();

            return first?.Uri.ToString()[(first.Uri.ToString().LastIndexOf("/", StringComparison.Ordinal) + 1)..];
        }

        public object ExecuteQuery(OntologyGraph graph, string text)
        {
            SparqlParameterizedString queryString = new()
            {
                CommandText = text
            };


            SparqlQueryParser parser = new SparqlQueryParser();
            SparqlQuery query = parser.ParseFromString(queryString);

            var result = (SparqlResultSet) graph.ExecuteQuery(query);


            StringBuilder builder = new();

            foreach (var res in result.Results)
            {
                if (res.ToString().Contains("Thing"))
                {
                    // add res remove all symbols before thing
                    builder.Append(res.ToString().Substring(res.ToString().IndexOf("Thing", StringComparison.Ordinal)))
                        .Append("\n");
                }
            }


            return builder.ToString();
        }

        private IUriNode EnsureRelation(OntologyGraph graph, string relationName)
        {
            var node = graph.AllNodes.Where(x => x.NodeType == NodeType.Uri).Select(x => (IUriNode) x).ToList();

            var first = node.FirstOrDefault(x => x.Uri.ToString().Equals(DefaultLink + "relation/" + relationName));

            if (first == null)
            {
                first = graph.CreateUriNode(new Uri(DefaultLink + "relation/" + relationName));
            }

            return first;
        }

        public void AddTriple(OntologyGraph graph, string objectName, string subjectName, string relation)
        {
            UriNode objectNode = FindUriNode(graph, objectName);
            UriNode subjectNode = FindUriNode(graph, subjectName[1..]);
            IUriNode relationNode = EnsureRelation(graph, relation);

            if (objectNode != null && subjectNode != null && relationNode != null)
            {
                Triple triple = new Triple(subjectNode, relationNode, objectNode);

                graph.Assert(triple);
            }
        }
    }
}