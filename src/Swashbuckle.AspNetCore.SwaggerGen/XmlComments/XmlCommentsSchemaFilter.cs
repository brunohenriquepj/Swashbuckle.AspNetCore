﻿using System;
using System.ComponentModel;
using System.Xml.XPath;
using System.Reflection;
using Microsoft.OpenApi.Any;
using Newtonsoft.Json.Serialization;
using Microsoft.OpenApi.Models;

namespace Swashbuckle.AspNetCore.SwaggerGen
{
    public class XmlCommentsSchemaFilter : ISchemaFilter
    {
        private const string MemberXPath = "/doc/members/member[@name='{0}']";
        private const string SummaryTag = "summary";
        private const string ExampleXPath = "example";

        private readonly XPathNavigator _xmlNavigator;

        public XmlCommentsSchemaFilter(XPathDocument xmlDoc)
        {
            _xmlNavigator = xmlDoc.CreateNavigator();
        }

        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            var jsonObjectContract = context.JsonContract as JsonObjectContract;
            if (jsonObjectContract == null) return;

            var memberName = XmlCommentsMemberNameHelper.GetMemberNameForType(context.Type);
            var typeNode = _xmlNavigator.SelectSingleNode(string.Format(MemberXPath, memberName));

            if (typeNode != null)
            {
                var summaryNode = typeNode.SelectSingleNode(SummaryTag);
                if (summaryNode != null)
                    schema.Description = XmlCommentsTextHelper.Humanize(summaryNode.InnerXml);
            }

            if (schema.Properties == null) return;
            foreach (var entry in schema.Properties)
            {
                var jsonProperty = jsonObjectContract.Properties[entry.Key];
                if (jsonProperty == null) continue;

                if (jsonProperty.TryGetMemberInfo(out MemberInfo memberInfo))
                {
                    ApplyPropertyComments(entry.Value, memberInfo);
                }
            }
        }

        private void ApplyPropertyComments(OpenApiSchema propertySchema, MemberInfo memberInfo)
        {
            var memberName = XmlCommentsMemberNameHelper.GetMemberNameForMember(memberInfo);
            var memberNode = _xmlNavigator.SelectSingleNode(string.Format(MemberXPath, memberName));
            if (memberNode == null) return;

            var summaryNode = memberNode.SelectSingleNode(SummaryTag);
            if (summaryNode != null)
            {
                propertySchema.Description = XmlCommentsTextHelper.Humanize(summaryNode.InnerXml);
            }

            var exampleNode = memberNode.SelectSingleNode(ExampleXPath);
            if (exampleNode != null)
            {
                var exampleString = XmlCommentsTextHelper.Humanize(exampleNode.InnerXml);
                var memberType = (memberInfo.MemberType & MemberTypes.Field) != 0 ? ((FieldInfo) memberInfo).FieldType : ((PropertyInfo) memberInfo).PropertyType;
                propertySchema.Example = ConvertToOpenApiType(exampleString, memberType);
            }
        }

        private static IOpenApiAny ConvertToOpenApiType(string value, Type type)
        {
            object typedExample;

            try
            {
                typedExample = TypeDescriptor.GetConverter(type).ConvertFrom(value);
            }
            catch (Exception)
            {
                return new OpenApiString(value);
            }

            return OpenApiAnyFactory.TryCreateFrom(typedExample, out IOpenApiAny openApiAny)
                ? openApiAny
                : new OpenApiString(value);
        }
    }
}
