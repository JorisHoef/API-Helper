using System;
using Newtonsoft.Json.Serialization;

namespace Deucarian.API.Configuration
{
    /// <summary>Controls how DTO property names are represented in JSON.</summary>
    public enum ApiJsonPropertyNamingPolicy
    {
        SnakeCase = 0,
        CamelCase = 1,
        PascalCase = 2,
        KebabCase = 3,
        ScreamingSnakeCase = 4,
        AsDeclared = 5
    }

    internal static class ApiJsonPropertyNamingStrategyFactory
    {
        public static NamingStrategy Create(ApiJsonPropertyNamingPolicy policy)
        {
            NamingStrategy strategy;
            switch (policy)
            {
                case ApiJsonPropertyNamingPolicy.CamelCase:
                    strategy = new CamelCaseNamingStrategy();
                    break;
                case ApiJsonPropertyNamingPolicy.PascalCase:
                    strategy = new PascalCaseNamingStrategy();
                    break;
                case ApiJsonPropertyNamingPolicy.KebabCase:
                    strategy = new KebabCaseNamingStrategy();
                    break;
                case ApiJsonPropertyNamingPolicy.ScreamingSnakeCase:
                    strategy = new ScreamingSnakeCaseNamingStrategy();
                    break;
                case ApiJsonPropertyNamingPolicy.AsDeclared:
                    strategy = new DefaultNamingStrategy();
                    break;
                case ApiJsonPropertyNamingPolicy.SnakeCase:
                default:
                    strategy = new SnakeCaseNamingStrategy();
                    break;
            }

            strategy.OverrideSpecifiedNames = false;
            strategy.ProcessDictionaryKeys = false;
            strategy.ProcessExtensionDataNames = false;
            return strategy;
        }

        private sealed class PascalCaseNamingStrategy : NamingStrategy
        {
            private readonly CamelCaseNamingStrategy camelCase =
                    new CamelCaseNamingStrategy();

            protected override string ResolvePropertyName(string name)
            {
                string value = camelCase.GetPropertyName(name, false);
                if (string.IsNullOrEmpty(value) || char.IsUpper(value[0]))
                {
                    return value;
                }

                return char.ToUpperInvariant(value[0]) + value.Substring(1);
            }
        }

        private sealed class ScreamingSnakeCaseNamingStrategy : NamingStrategy
        {
            private readonly SnakeCaseNamingStrategy snakeCase =
                    new SnakeCaseNamingStrategy();

            protected override string ResolvePropertyName(string name)
            {
                return snakeCase.GetPropertyName(name, false)
                                ?.ToUpperInvariant();
            }
        }
    }
}
