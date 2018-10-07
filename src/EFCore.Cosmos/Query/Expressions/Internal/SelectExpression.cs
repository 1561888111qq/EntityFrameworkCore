﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Cosmos.Query.ExpressionVisitors.Internal;
using Microsoft.EntityFrameworkCore.Cosmos.Storage;
using Microsoft.EntityFrameworkCore.Cosmos.Storage.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Newtonsoft.Json.Linq;
using Remotion.Linq.Clauses;
using Remotion.Linq.Clauses.Expressions;

namespace Microsoft.EntityFrameworkCore.Cosmos.Query.Expressions.Internal
{
    public class SelectExpression : Expression
    {
        private const string _rootAlias = "c";
        private readonly IQuerySource _querySource;

        public EntityProjectionExpression Projection { get; }
        public Expression FromExpression { get; }
        public Expression FilterExpression { get; private set; }

        public SelectExpression(IEntityType entityType, IQuerySource querySource)
        {
            Projection = new EntityProjectionExpression(entityType, _rootAlias);
            FromExpression = new RootReferenceExpression(entityType, _rootAlias);
            EntityType = entityType;
            FilterExpression = GetDiscriminatorPredicate(entityType);
            _querySource = querySource;
        }

        public BinaryExpression GetDiscriminatorPredicate(IEntityType entityType)
        {
            if (!EntityType.IsAssignableFrom(entityType))
            {
                return null;
            }

            var discriminatorProperty = entityType.Cosmos().DiscriminatorProperty;

            return MakeBinary(
                           ExpressionType.Equal,
                           new KeyAccessExpression(discriminatorProperty, FromExpression),
                           Constant(entityType.Cosmos().DiscriminatorValue, discriminatorProperty.ClrType));
        }

        public Expression BindPropertyPath(
            QuerySourceReferenceExpression querySourceReferenceExpression, List<IPropertyBase> properties)
        {
            if (querySourceReferenceExpression == null
                || querySourceReferenceExpression.ReferencedQuerySource != _querySource)
            {
                return null;
            }

            var currentExpression = FromExpression;

            foreach (var property in properties)
            {
                currentExpression = new KeyAccessExpression(property, currentExpression);
            }

            return currentExpression;
        }

        public void AddToPredicate(Expression predicate)
        {
            FilterExpression = AndAlso(FilterExpression, predicate);
        }

        public override Type Type => typeof(JObject);
        public override ExpressionType NodeType => ExpressionType.Extension;

        public IEntityType EntityType { get; }

        public override string ToString()
            => new CosmosSqlGenerator().GenerateSqlQuerySpec(this, new Dictionary<string, object>()).Query;

        public CosmosSqlQuery ToSqlQuery(IReadOnlyDictionary<string, object> parameterValues)
            => new CosmosSqlGenerator().GenerateSqlQuerySpec(this, parameterValues);
    }
}
