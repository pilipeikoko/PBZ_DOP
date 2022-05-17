using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF.Ontology;

namespace OwlGenerator
{
    internal class ScsService
    {
        public string ConvertToScs(List<(string Class, string Language, string Parent)> classesWithParent,
            List<(string Individual, string Parent)> individualsWithParent,
            List<(string Object, string Predicate, string Subject)> relations)
        {
            StringBuilder builder = new StringBuilder();

            foreach (var customClass in classesWithParent)
            {
                builder.Append(CreateClass(customClass.Class));
                builder.Append(CreateIdentifier(customClass.Class, customClass.Language));
            }

            foreach (var customClass in classesWithParent)
            {
                builder.Append(CreateSubClass(customClass.Class, customClass.Parent));
            }

            foreach (var individual in individualsWithParent)
            {
                builder.Append(CreateIndividual(individual.Individual));
                builder.Append(CreateIdentifier(individual.Individual));
            }

            foreach (var individual in individualsWithParent)
            {
                builder.Append(AddIndividualToClass(individual.Individual, individual.Parent));
            }

            foreach (var relation in relations)
            {
                builder.Append(CreatePredicate(relation.Predicate));
                builder.Append(CreateTriples(relation.Object, relation.Predicate, relation.Subject,
                    classesWithParent.Select(x => x.Class).ToList()));
            }

            return builder.ToString();
        }

        private string CreateTriples(string relationObject, string relationPredicate, string relationSubject,
            List<string> classes)
        {
            string objectName = classes.Contains(GetClassName(relationObject))
                ? GetClassName(relationObject)
                : GetIndividualName(relationObject);

            string subjectName = classes.Contains(GetClassName(relationSubject))
                ? GetClassName(relationSubject)
                : GetIndividualName(relationSubject);

            string predicateName = GetPredicateName(relationPredicate);

            return $"{objectName}=>{predicateName}: {subjectName};;\n";
        }

        private string CreatePredicate(string predicate)
        {
            if (predicate.StartsWith("nrel"))
            {
                return $"{GetPredicateName(predicate)}<-sc_node_norole_relation;;\n";
            }

            if (predicate.StartsWith("rrel"))
            {
                return $"{GetPredicateName(predicate)}<-sc_node_role_relation;;\n";
            }

            return string.Empty;
        }

        private string AddIndividualToClass(string individualIndividual, string individualParent)
        {
            var parent = GetClassName(individualParent);
            var individual = GetIndividualName(individualIndividual);

            return $"{parent}->{individual};;\n";
        }

        private string CreateIdentifier(string customClassClass, string customClassLanguage = null)
        {
            return customClassLanguage != null
                ? $"{GetClassName(customClassClass)}=>nrel_main_idtf:[{customClassClass}](*<-lang_{customClassLanguage};;*);;\n"
                : $"{GetClassName(customClassClass)}=>nrel_main_idtf:[{customClassClass}];;\n";
        }

        private string CreateSubClass(string customClassClass, string customClassParent)
        {
            if (customClassParent != null)
            {
                string className = GetClassName(customClassClass);
                string parentName = GetClassName(customClassParent);

                return $"{parentName}->{className};;\n";
            }

            return string.Empty;
        }

        private string CreateClass(string customClassClass)
        {
            string className = GetClassName(customClassClass);

            return $"{className}<-sc_node_not_relation;;\n";
        }

        private string CreateIndividual(string individualIndividual)
        {
            string name = GetIndividualName(individualIndividual);

            return $"{name}<-sc_node_not_relation;;\n";
        }

        private string GetClassName(string name)
        {
            return "concept_" + name.Replace(" ", "_").ToLower();
        }

        private string GetIndividualName(string name)
        {
            return name.Replace(" ", "_").ToLower();
        }

        private string GetPredicateName(string name)
        {
            return name.Replace(" ", "_").ToLower();
        }
    }
}