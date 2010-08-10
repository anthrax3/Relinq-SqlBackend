// This file is part of the re-motion Core Framework (www.re-motion.org)
// Copyright (C) 2005-2009 rubicon informationstechnologie gmbh, www.rubicon.eu
// 
// The re-motion Core Framework is free software; you can redistribute it 
// and/or modify it under the terms of the GNU Lesser General Public License 
// as published by the Free Software Foundation; either version 2.1 of the 
// License, or (at your option) any later version.
// 
// re-motion is distributed in the hope that it will be useful, 
// but WITHOUT ANY WARRANTY; without even the implied warranty of 
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the 
// GNU Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public License
// along with re-motion; if not, see http://www.gnu.org/licenses.
// 
using System;
using System.Linq.Expressions;
using System.Reflection;
using Remotion.Data.Linq.Utilities;

namespace Remotion.Data.Linq.SqlBackend.SqlPreparation.MethodCallTransformers
{
  /// <summary>
  /// <see cref="EqualMethodCallTransformer"/> implements <see cref="IMethodCallTransformer"/> for the Equal method.
  /// </summary>
  public class EqualMethodCallTransformer : IMethodCallTransformer
  {
    public static readonly string[] SupportedMethodNames = new[] { "Equals" };

    public Expression Transform (MethodCallExpression methodCallExpression)
    {
      ArgumentUtility.CheckNotNull ("methodCallExpression", methodCallExpression);

      if (methodCallExpression.Arguments.Count == 1)
      {
        MethodCallTransformerUtility.CheckInstanceMethod (methodCallExpression);

        return Expression.Equal (methodCallExpression.Object, methodCallExpression.Arguments[0]);
      }
      else if (methodCallExpression.Arguments.Count == 2)
      {
        MethodCallTransformerUtility.CheckStaticMethod (methodCallExpression);

        return Expression.Equal (methodCallExpression.Arguments[0], methodCallExpression.Arguments[1]);
      }

      throw new NotSupportedException (
            string.Format (
                "{0} function with {1} arguments is not supported.", methodCallExpression.Method.Name, methodCallExpression.Arguments.Count));
    }
  }
}