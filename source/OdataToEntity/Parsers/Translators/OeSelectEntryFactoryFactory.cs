﻿using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace OdataToEntity.Parsers.Translators
{
    internal sealed class OeSelectEntryFactoryFactory : OeEntryFactoryFactory
    {
        private readonly OeNavigationSelectItem _rootNavigationItem;

        public OeSelectEntryFactoryFactory(OeNavigationSelectItem rootNavigationItem)
        {
            _rootNavigationItem = rootNavigationItem;
        }

        public override OeEntryFactory CreateEntryFactory(IEdmEntitySet entitySet, Type clrType, OePropertyAccessor[] skipTokenAccessors)
        {
            ParameterExpression parameter = Expression.Parameter(typeof(Object));
            UnaryExpression typedParameter = Expression.Convert(parameter, clrType);

            if (!_rootNavigationItem.HasNavigationItems())
            {
                var navigationLinks = new OeEntryFactory[_rootNavigationItem.NavigationItems.Count];
                for (int i = 0; i < _rootNavigationItem.NavigationItems.Count; i++)
                {
                    OeNavigationSelectItem navigationItem = _rootNavigationItem.NavigationItems[i];
                    var nextLinkOptions = new OeEntryFactoryOptions()
                    {
                        Accessors = Array.Empty<OePropertyAccessor>(),
                        EdmNavigationProperty = navigationItem.EdmProperty,
                        EntitySet = navigationItem.EntitySet,
                        NavigationSelectItem = navigationItem.NavigationSelectItem,
                        NextLink = navigationItem.Kind == OeNavigationSelectItemKind.NextLink
                    };
                    navigationLinks[i] = new OeEntryFactory(ref nextLinkOptions);
                }

                var options = new OeEntryFactoryOptions()
                {
                    Accessors = GetAccessors(clrType, _rootNavigationItem),
                    EntitySet = _rootNavigationItem.EntitySet,
                    NavigationLinks = navigationLinks,
                    SkipTokenAccessors = skipTokenAccessors
                };
                _rootNavigationItem.EntryFactory = new OeEntryFactory(ref options);
            }
            else
            {
                List<OeNavigationSelectItem> navigationItems = OeSelectTranslator.FlattenNavigationItems(_rootNavigationItem, true);
                IReadOnlyList<MemberExpression> navigationProperties = OeExpressionHelper.GetPropertyExpressions(typedParameter);
                int propertyIndex = navigationProperties.Count - 1;

                for (int i = navigationItems.Count - 1; i >= 0; i--)
                {
                    OeNavigationSelectItem navigationItem = navigationItems[i];
                    if (navigationItem.Kind == OeNavigationSelectItemKind.NotSelected)
                    {
                        propertyIndex--;
                        continue;
                    }

                    OePropertyAccessor[] accessors = Array.Empty<OePropertyAccessor>();
                    LambdaExpression? linkAccessor = null;
                    OeEntryFactory[] nestedNavigationLinks = Array.Empty<OeEntryFactory>();
                    if (navigationItem.Kind != OeNavigationSelectItemKind.NextLink)
                    {
                        accessors = GetAccessors(navigationProperties[propertyIndex].Type, navigationItem);
                        linkAccessor = Expression.Lambda(navigationProperties[propertyIndex], parameter);
                        nestedNavigationLinks = GetNestedNavigationLinks(navigationItem);
                        propertyIndex--;
                    }

                    var options = new OeEntryFactoryOptions()
                    {
                        Accessors = accessors,
                        EdmNavigationProperty = navigationItem.Parent == null ? null : navigationItem.EdmProperty,
                        EntitySet = navigationItem.EntitySet,
                        LinkAccessor = linkAccessor,
                        NavigationLinks = nestedNavigationLinks,
                        NavigationSelectItem = navigationItem.Parent == null ? null : navigationItem.NavigationSelectItem,
                        NextLink = navigationItem.Kind == OeNavigationSelectItemKind.NextLink,
                        SkipTokenAccessors = skipTokenAccessors
                    };
                    navigationItem.EntryFactory = new OeEntryFactory(ref options);
                }
            }

            return _rootNavigationItem.EntryFactory;
        }
        private static OePropertyAccessor[] GetAccessors(Type clrEntityType, OeNavigationSelectItem navigationItem)
        {
            if (navigationItem.AllSelected)
                return OePropertyAccessor.CreateFromType(clrEntityType, navigationItem.EntitySet);

            IReadOnlyList<OeStructuralSelectItem> structuralItems = navigationItem.GetStructuralItemsWithNotSelected();
            var accessors = new OePropertyAccessor[structuralItems.Count];

            ParameterExpression parameter = Expression.Parameter(typeof(Object));
            UnaryExpression typedAccessorParameter = Expression.Convert(parameter, clrEntityType);
            IReadOnlyList<MemberExpression> propertyExpressions = OeExpressionHelper.GetPropertyExpressions(typedAccessorParameter);
            for (int i = 0; i < structuralItems.Count; i++)
                accessors[i] = OePropertyAccessor.CreatePropertyAccessor(structuralItems[i].EdmProperty, propertyExpressions[i], parameter, structuralItems[i].NotSelected);

            return accessors;
        }
        private static OeEntryFactory[] GetNestedNavigationLinks(OeNavigationSelectItem navigationItem)
        {
            var nestedEntryFactories = new List<OeEntryFactory>(navigationItem.NavigationItems.Count);
            for (int i = 0; i < navigationItem.NavigationItems.Count; i++)
                if (navigationItem.NavigationItems[i].Kind != OeNavigationSelectItemKind.NotSelected)
                    nestedEntryFactories.Add(navigationItem.NavigationItems[i].EntryFactory);
            return nestedEntryFactories.ToArray();
        }
    }
}
