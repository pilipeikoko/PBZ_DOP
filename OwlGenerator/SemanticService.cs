using System;
using System.Collections.Generic;
using System.Linq;
using Ostis.Sctp;
using Ostis.Sctp.Arguments;
using Ostis.Sctp.Commands;
using Ostis.Sctp.Responses;
using Ostis.Sctp.Tools;

namespace OwlGenerator
{
    public class SemanticService
    {
        private SctpClient sctpClient;

        public SemanticService()
        {
            const string defaultAddress = SctpProtocol.TestServerIp;
            string serverAddress = defaultAddress;
            int serverPort = SctpProtocol.DefaultPortNumber;

            sctpClient = new SctpClient(serverAddress, serverPort);

            sctpClient.Connect();
        }

        public void AddToScMemory(List<(string ClassName, string Language, string Parent)> classesWithParent,
            List<(string Individual, string Parent)> individualsWithParent,
            List<(string Object, string RelationName, string Subject)> relations)
        {
            foreach (var customClass in classesWithParent)
            {
                var createdClass = CreateClass(GetClassName(customClass.ClassName));

                AddToSet("sc_node_not_relation", createdClass);
                AddIdentifier(createdClass, customClass.ClassName, "nrel_main_idtf",
                    "lang_" + customClass.Language);
            }

            foreach (var customClass in classesWithParent)
            {
                if (!string.IsNullOrEmpty(customClass.Parent) && !string.IsNullOrEmpty(customClass.ClassName))
                {
                    AddToSet(GetClassName(customClass.Parent), GetClassName(customClass.ClassName));
                }
            }

            foreach (var individual in individualsWithParent)
            {
                var individualNode = CreateIndividual(individual.Individual);

                AddToSet(GetClassName(individual.Parent), GetIndividualName(individual.Individual));
                AddIdentifier(individualNode, individual.Individual, "nrel_main_idtf");
            }

            var classes = classesWithParent.Select(x => x.ClassName).ToList();

            foreach (var relation in relations)
            {
                ScAddress relationNode = CreateRelation(GetRelationName(relation.RelationName));

                string objectName = classes.Any(x => GetClassName(x) == GetClassName(relation.Object))
                    ? GetClassName(relation.Object)
                    : GetIndividualName(relation.Object);
                ScAddress objectNode = FindAddressByIdentifier(objectName);

                string subjectName = classes.Any(x => GetClassName(x) == GetClassName(relation.Subject))
                    ? GetClassName(relation.Subject)
                    : GetIndividualName(relation.Subject);
                ScAddress subjectNode = FindAddressByIdentifier(subjectName);

                CreateFiveConstruction(objectNode, ElementType.ConstantCommonArc_c, subjectNode,
                    ElementType.PositiveConstantPermanentAccessArc_c, relationNode);
            }
        }

        private ScAddress CreateRelation(string relation)
        {
            ElementType type = ElementType.Constant_a;
            type.AddType(ElementType.Node_a);
            type.AddType(ElementType.PermanentArc_a);

            string superClassName = string.Empty;

            if (relation.StartsWith("nrel"))
            {
                superClassName = "sc_node_norole_relation";
                type.AddType(ElementType.NonRoleNode_a);
            }
            else if (relation.StartsWith("rrel"))
            {
                superClassName = "sc_node_role_relation";
                type.AddType(ElementType.RoleNode_a);
            }

            var node = CreateNodeWithSystemIdentifier(relation, type);
            if (!string.IsNullOrEmpty(superClassName))
            {
                AddToSet(superClassName, node);
            }

            return node;
        }

        private bool AddIdentifier(ScAddress node, string content, string identifierType, string language = "lang_en")
        {
            ScAddress identifierNode = FindAddressByIdentifier(identifierType);
            ScAddress languageNode = FindAddressByIdentifier(language);
            ScAddress linkNode = CreateLink(content);

            return CreateFiveConstruction(node, ElementType.ConstantCommonArc_c, linkNode,
                       ElementType.PositiveConstantPermanentAccessArc_c, identifierNode) &&
                   CreateThreeConstruction(languageNode, ElementType.PositiveConstantPermanentAccessArc_c, linkNode);
        }

        private ScAddress CreateClass(string className)
        {
            ElementType type = ElementType.ConstantNode_c;
            type.AddType(ElementType.ClassNode_a);
            var node = CreateNodeWithSystemIdentifier(className, type);

            return node;
        }

        private ScAddress CreateIndividual(string individual)
        {
            string name = GetIndividualName(individual);

            ElementType type = ElementType.ConstantNode_c;
            var node = CreateNodeWithSystemIdentifier(name, type);

            return node;
        }

        private bool AddToSet(string parentSystemIdentifier, string childSystemIdentifier)
        {
            var parentNode = FindAddressByIdentifier(parentSystemIdentifier);
            var childNode = FindAddressByIdentifier(childSystemIdentifier);

            return AddToSet(parentNode, childNode);
        }

        private bool AddToSet(string parentSystemIdentifier, ScAddress childNode)
        {
            var parentNode = FindAddressByIdentifier(parentSystemIdentifier);

            return AddToSet(parentNode, childNode);
        }

        private bool AddToSet(ScAddress parentNode, ScAddress node)
        {
            return CreateThreeConstruction(parentNode, ElementType.PositiveConstantPermanentAccessArc_c, node);
        }

        private ScAddress CreateNodeWithSystemIdentifier(string systemIdentifier, ElementType type)
        {
            var command = new CreateNodeCommand(type);
            var response = (CreateNodeResponse)sctpClient.Send(command);

            var command2 = new SetSystemIdCommand(response?.CreatedNodeAddress, new Identifier(systemIdentifier));
            var response2 = (SetSystemIdResponse)sctpClient.Send(command2);

            return response.CreatedNodeAddress;
        }

        private string GetClassName(string name)
        {
            return name.StartsWith("concept_")
                ? name.Replace(" ", "_").ToLower()
                : "concept_" + name.Replace(" ", "_").ToLower();
        }

        private string GetIndividualName(string name)
        {
            return name.Replace(" ", "_").ToLower();
        }

        private string GetRelationName(string name)
        {
            return name.Replace(" ", "_").ToLower();
        }

        private ScAddress FindAddressByIdentifier(string identifier)
        {
            var command = new FindElementCommand(identifier);
            var response = (FindElementResponse)sctpClient.Send(command);

            return response?.FoundAddress;
        }

        private ScAddress CreateLink(string content)
        {
            var command = new CreateLinkCommand();
            var response = (CreateLinkResponse)sctpClient.Send(command);

            var command1 = new SetLinkContentCommand(response.CreatedLinkAddress, new LinkContent(content));
            var response1 = (SetLinkContentResponse)sctpClient.Send(command1);

            return response1.ContentIsSet ? response?.CreatedLinkAddress : ScAddress.Invalid;
        }

        private bool CreateFiveConstruction(ScAddress first, ElementType second, ScAddress third, ElementType fourth,
            ScAddress fifth)
        {
            var command = new CreateArcCommand(second, first, third);
            var response = (CreateArcResponse)sctpClient.Send(command);

            var command1 = new CreateArcCommand(fourth, fifth, response.CreatedArcAddress);
            var response1 = (CreateArcResponse)sctpClient.Send(command1);

            return response.CreatedArcAddress.IsValid && response1.CreatedArcAddress.IsValid;
        }

        private bool CreateThreeConstruction(ScAddress parentNode, ElementType positiveConstantPermanentAccessArcC,
            ScAddress node)
        {
            var command = new CreateArcCommand(positiveConstantPermanentAccessArcC, parentNode, node);
            var response = (CreateArcResponse)sctpClient.Send(command);

            return response.CreatedArcAddress.IsValid;
        }
    }
}