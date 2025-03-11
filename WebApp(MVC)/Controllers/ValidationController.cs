using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using System.Text;

namespace WebApp_MVC_.Controllers
{
    public class ValidationController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost("api/validation/validate")]
        public async Task<IActionResult> ValidateForm([FromForm] Models.JsonRequest request, IFormFile reactFormFile)
        {
            try
            {
                string? jsxCode = request?.JsxCode;

                if (reactFormFile != null)
                {
                    using (var reader = new StreamReader(reactFormFile.OpenReadStream()))
                    {
                        jsxCode = await reader.ReadToEndAsync();
                    }
                }

                if (string.IsNullOrWhiteSpace(jsxCode))
                {
                    return BadRequest("The jsxCode field or file is required.");
                }

                // Extract components first
                var components = ExtractComponents(jsxCode);
                if (!components.Any())
                {
                    return BadRequest("No valid React components found.");
                }

                var result = new Dictionary<string, object>();
                foreach (var component in components)
                {
                    var validationResult = ValidateReactCode(component.Code);
                    if (!validationResult.IsValid)
                    {
                        return BadRequest(new
                        {
                            componentName = component.Name,
                            message = "Invalid React Code",
                            errors = validationResult.Errors
                        });
                    }

                    var validationRules = ExtractValidationRules(component.Code);

                    // Add component metadata
                    result[component.Name] = new Dictionary<string, object>
                    {
                        ["validationRules"] = validationRules,
                        ["metadata"] = new
                        {
                            totalFields = validationRules.Count,
                            componentType = component.Type
                        }
                    };
                }

                // Add global metadata
                var globalMetadata = new
                {
                    totalComponents = components.Count,
                    totalFields = components.Sum(c =>
                        ((Dictionary<string, object>)((Dictionary<string, object>)result[c.Name])["validationRules"]).Count)
                };

                var finalResult = new Dictionary<string, object>
                {
                    ["components"] = result,
                    ["metadata"] = globalMetadata
                };

                string jsonResult = JsonConvert.SerializeObject(finalResult, Formatting.Indented);
                return Ok(jsonResult);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error processing the form: {ex.Message}");
            }
        }


        [HttpPost("api/validation/convert-to-model")]
        public IActionResult ConvertToModel([FromBody] string jsonValidation)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(jsonValidation))
                {
                    return BadRequest("JSON validation data is required.");
                }

                Console.WriteLine(jsonValidation);
                var validationData = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonValidation);
                if (validationData == null || !validationData.ContainsKey("components"))
                {
                    return BadRequest("Invalid JSON structure. The 'components' key is missing.");
                }
                var components = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                    JsonConvert.SerializeObject(validationData["components"]));
                if (components == null || !components.Any())
                {
                    return BadRequest("No components found in the validation data.");
                }

                var modelCode = GenerateCSharpModels(components);

                // Option 1: Return as downloadable file
                byte[] byteArray = Encoding.UTF8.GetBytes(modelCode);
                var fileName = "ValidationModel.cs";
                return File(byteArray, "text/plain", fileName);

                // Option 2: If you still want to return JSON with the model code
                // return Ok(new { modelCode, fileName = "ValidationModel.cs" });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error converting to model: {ex.Message}");
            }
        }


        private string GenerateCSharpModels(Dictionary<string, object> components)
        {
            var sb = new StringBuilder();

            // Add necessary namespaces
            sb.AppendLine("using System;");
            sb.AppendLine("using System.ComponentModel.DataAnnotations;");
            sb.AppendLine();
            sb.AppendLine("namespace WebApp_MVC_.Models");
            sb.AppendLine("{");

            foreach (var component in components)
            {
                var componentName = component.Key;
                var componentData = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                    JsonConvert.SerializeObject(component.Value));

                if (componentData != null && componentData.ContainsKey("validationRules"))
                {
                    var validationRules = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                        JsonConvert.SerializeObject(componentData["validationRules"]));

                    if (validationRules != null && validationRules.Any())
                    {
                        // Generate class
                        sb.AppendLine($"    public class {componentName}Model");
                        sb.AppendLine("    {");

                        foreach (var field in validationRules)
                        {
                            var fieldName = field.Key;
                            var fieldRules = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                                JsonConvert.SerializeObject(field.Value));

                            if (fieldRules != null)
                            {
                                // Add validation attributes
                                foreach (var rule in fieldRules)
                                {
                                    if (rule.Key != "fieldMetadata")
                                    {
                                        var ruleDetails = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                                            JsonConvert.SerializeObject(rule.Value));

                                        if (ruleDetails != null && ruleDetails.ContainsKey("message"))
                                        {
                                            string message = ruleDetails["message"].ToString();
                                            string attribute = MapToValidationAttribute(rule.Key, ruleDetails, message);
                                            sb.AppendLine($"        {attribute}");
                                        }
                                    }
                                }

                                // Determine property type
                                string propertyType = DeterminePropertyType(fieldRules);

                                // Add property
                                sb.AppendLine($"        public {propertyType} {CapitalizeFirstLetter(fieldName)} {{ get; set; }}");
                                sb.AppendLine();
                            }
                        }

                        sb.AppendLine("    }");
                        sb.AppendLine();
                    }
                }
            }

            sb.AppendLine("}");

            return sb.ToString();
        }

        private string CapitalizeFirstLetter(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return char.ToUpper(input[0]) + input.Substring(1);
        }

        private string DeterminePropertyType(Dictionary<string, object> fieldRules)
        {
            // Default type is string
            string propertyType = "string";

            // Check for specific types based on validation rules
            if (fieldRules.ContainsKey("email"))
            {
                propertyType = "string";
            }
            else if (fieldRules.ContainsKey("password"))
            {
                propertyType = "string";
            }
            else if (fieldRules.ContainsKey("minLength") || fieldRules.ContainsKey("maxLength"))
            {
                propertyType = "string";
            }

            // For numeric fields, we might infer from other validation rules
            // This would require more complex logic

            return propertyType;
        }

        private string MapToValidationAttribute(string ruleType, Dictionary<string, object> ruleDetails, string message)
        {
            switch (ruleType.ToLower())
            {
                case "required":
                    return $"[Required(ErrorMessage = \"{message}\")]";

                case "email":
                    return $"[EmailAddress(ErrorMessage = \"{message}\")]";

                case "minlength":
                    int minLength = Convert.ToInt32(ruleDetails["value"]);
                    return $"[MinLength({minLength}, ErrorMessage = \"{message}\")]";

                case "maxlength":
                    int maxLength = Convert.ToInt32(ruleDetails["value"]);
                    return $"[MaxLength({maxLength}, ErrorMessage = \"{message}\")]";

                case "pattern":
                    string pattern = ruleDetails["value"].ToString();
                    return $"[RegularExpression(@\"{pattern}\", ErrorMessage = \"{message}\")]";

                case "password":
                    return $"[DataType(DataType.Password, ErrorMessage = \"{message}\")]";

                default:
                    return string.Empty;
            }
        }


        private class Component
        {
            public string Name { get; set; }
            public string Code { get; set; }
            public string Type { get; set; }  // "class" or "function"
        }

        private List<Component> ExtractComponents(string jsxCode)
        {
            var components = new List<Component>();

            // Updated patterns to better match React components
            var patterns = new Dictionary<string, (string pattern, string type)>
            {
                ["class"] = (@"class\s+(\w+)\s+extends\s+React\.Component\s*{(.*?)}", "class"),
                ["functionDeclaration"] = (@"function\s+(\w+)\s*\([^)]*\)\s*{(.*?)return\s*\((.*?)\);?\s*}", "function"),
                ["arrowFunction"] = (@"const\s+(\w+)\s*=\s*\([^)]*\)\s*=>\s*{(.*?)return\s*\((.*?)\);?\s*}", "function"),
                ["arrowFunctionImplicit"] = (@"const\s+(\w+)\s*=\s*\([^)]*\)\s*=>\s*\((.*?)\)", "function")
            };

            foreach (var pattern in patterns)
            {
                var regex = new Regex(pattern.Value.pattern, RegexOptions.Singleline);
                var matches = regex.Matches(jsxCode);

                foreach (Match match in matches)
                {
                    var componentName = match.Groups[1].Value;

                    // Skip if component name is handleSubmit or validateForm
                    if (componentName.ToLower().Contains("handle") ||
                        componentName.ToLower().Contains("validate"))
                    {
                        continue;
                    }

                    // Get the full component code
                    var componentCode = match.Value;

                    // Only add if it's a valid React component (contains JSX or return statement)
                    if (componentCode.Contains("return") ||
                        componentCode.Contains("<") && componentCode.Contains("/>"))
                    {
                        components.Add(new Component
                        {
                            Name = componentName,
                            Code = componentCode,
                            Type = pattern.Value.type
                        });

                        // Look for nested function components
                        var nestedComponents = ExtractNestedComponents(componentCode);
                        components.AddRange(nestedComponents);
                    }
                }
            }

            return components.Distinct(new ComponentNameComparer()).ToList();
        }

        private List<Component> ExtractNestedComponents(string code)
        {
            var components = new List<Component>();

            // Pattern to match function components declared inside other components
            var nestedPatterns = new[]
            {
                @"function\s+(\w+)\s*\([^)]*\)\s*{(.*?)return\s*\((.*?)\);?\s*}",
                @"const\s+(\w+)\s*=\s*\([^)]*\)\s*=>\s*{(.*?)return\s*\((.*?)\);?\s*}",
                @"const\s+(\w+)\s*=\s*\([^)]*\)\s*=>\s*\((.*?)\)"
            };

            foreach (var pattern in nestedPatterns)
            {
                var regex = new Regex(pattern, RegexOptions.Singleline);
                var matches = regex.Matches(code);

                foreach (Match match in matches)
                {
                    var componentName = match.Groups[1].Value;

                    // Skip if component name is handleSubmit or validateForm
                    if (componentName.ToLower().Contains("handle") ||
                        componentName.ToLower().Contains("validate"))
                    {
                        continue;
                    }

                    // Get the full component code
                    var componentCode = match.Value;

                    // Only add if it's a valid React component
                    if (componentCode.Contains("return") ||
                        (componentCode.Contains("<") && componentCode.Contains("/>")))
                    {
                        components.Add(new Component
                        {
                            Name = componentName,
                            Code = componentCode,
                            Type = "function"
                        });
                    }
                }
            }

            return components;
        }

        // to ensure components with same name are not added multiple times
        private class ComponentNameComparer : IEqualityComparer<Component>
        {
            public bool Equals(Component x, Component y)
            {
                return x.Name == y.Name;
            }

            public int GetHashCode(Component obj)
            {
                return obj.Name.GetHashCode();
            }
        }

        private ValidationResult ValidateReactCode(string code)
        {
            var result = new ValidationResult { IsValid = true };

            try
            {
                if (!IsValidReactComponent(code))
                {
                    result.IsValid = false;
                    result.Errors.Add("Invalid React component structure");
                }
                return result;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Validation error: {ex.Message}");
                return result;
            }
        }

        private class ValidationResult
        {
            public bool IsValid { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
        }

        private bool IsValidReactComponent(string code)
        {
            var functionComponent = new Regex(@"(function|const)\s+\w+\s*\([^)]*\)\s*{");
            var classComponent = new Regex(@"class\s+\w+\s+extends\s+React\.Component");
            var arrowFunction = new Regex(@"const\s+\w+\s*=\s*\([^)]*\)\s*=>");

            return functionComponent.IsMatch(code) ||
                   classComponent.IsMatch(code) ||
                   arrowFunction.IsMatch(code);
        }

        private Dictionary<string, object> ExtractValidationRules(string jsxCode)
        {
            var rules = new Dictionary<string, object>();
            var validationPatterns = GetValidationPatterns();

            // Extract field names with component context
            var fieldNamePattern = new Regex(@"(?:(?:formData|credentials|values|formValues)\.(\w+)|<TextField[^>]*name\s*=\s*[""'](\w+)[""'])");
            var fieldMatches = fieldNamePattern.Matches(jsxCode);
            var fieldNames = fieldMatches
                .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)
                .Distinct()
                .ToList();

            foreach (var fieldName in fieldNames)
            {
                var fieldRules = new Dictionary<string, object>();
                bool hasValidations = false;

                // Extract the label for the field
                var labelPattern = new Regex($@"<TextField[^>]*name\s*=\s*[""{fieldName}""][^>]*label\s*=\s*[""']([^""']*)[""']");
                var labelMatch = labelPattern.Match(jsxCode);
                var fieldLabel = labelMatch.Success ? labelMatch.Groups[1].Value : fieldName;

                foreach (var pattern in validationPatterns)
                {
                    var matches = pattern.Value.Matches(jsxCode);
                    foreach (Match match in matches)
                    {
                        bool isFieldMatch = match.Value.Contains($"name=\"{fieldName}\"") ||
                                          match.Value.Contains($".{fieldName}") ||
                                          (match.Groups.Count > 1 && match.Groups[1].Value == fieldName);

                        if (isFieldMatch)
                        {
                            hasValidations = true;
                            var validationInfo = ExtractValidationInfo(pattern.Key, match, fieldLabel);
                            if (!fieldRules.ContainsKey(pattern.Key))
                            {
                                fieldRules[pattern.Key] = validationInfo;
                            }
                        }
                    }
                }

                // Add field-specific metadata
                if (hasValidations)
                {
                    fieldRules["fieldMetadata"] = new
                    {
                        label = fieldLabel,
                        validationCount = fieldRules.Count - 1  // Subtract 1 to exclude fieldMetadata
                    };
                    rules[fieldName] = fieldRules;
                }
            }

            return rules;
        }

        private Dictionary<string, Regex> GetValidationPatterns()
        {
            return new Dictionary<string, Regex>
            {
                ["required"] = new Regex(@"(?:<TextField[^>]*required(?:={true}|\s|>)|if\s*\(\s*!(?:formData|credentials|values|formValues)\.(\w+)\s*\)|(\w+)\s+is\s+required)", RegexOptions.IgnoreCase),
                ["email"] = new Regex(@"(?:<TextField[^>]*type\s*=\s*[""']email[""']|<TextField[^>]*label\s*=\s*[""']Email[""']|if\s*\(\s*!(?:formData|credentials|values|formValues)\.(\w+)\.match\(.*?email.*?\))", RegexOptions.IgnoreCase),
                ["password"] = new Regex(@"(?:<TextField[^>]*type\s*=\s*[""']password[""']|<TextField[^>]*label\s*=\s*[""']Password[""']|if\s*\(\s*!(?:formData|credentials|values|formValues)\.(\w+)\.match\(.*?password.*?\))", RegexOptions.IgnoreCase),
                ["minLength"] = new Regex(@"(?:<TextField[^>]*minLength\s*=\s*{\s*(\d+)\s*}|if\s*\(\s*(?:formData|credentials|values|formValues)\.(\w+)\.length\s*<\s*(\d+))", RegexOptions.IgnoreCase),
                ["maxLength"] = new Regex(@"(?:<TextField[^>]*maxLength\s*=\s*{\s*(\d+)\s*}|if\s*\(\s*(?:formData|credentials|values|formValues)\.(\w+)\.length\s*>\s*(\d+))", RegexOptions.IgnoreCase),
                ["pattern"] = new Regex(@"(?:<TextField[^>]*pattern\s*=\s*[""'](.*?)[""']|if\s*\(\s*!(?:formData|credentials|values|formValues)\.(\w+)\.match\((.*?)\))", RegexOptions.IgnoreCase)
            };
        }

        private object ExtractValidationInfo(string validationType, Match match, string fieldLabel)
        {
            var message = $"{fieldLabel} validation failed";
            object value = true;

            switch (validationType)
            {
                case "minLength":
                    value = int.Parse(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value);
                    message = $"{fieldLabel} must be at least {value} characters";
                    break;
                case "maxLength":
                    value = int.Parse(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value);
                    message = $"{fieldLabel} cannot exceed {value} characters";
                    break;
                case "email":
                    message = $"{fieldLabel} must be a valid email address";
                    break;
                case "password":
                    message = $"{fieldLabel} must be a valid password";
                    break;
                case "pattern":
                    value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;
                    message = $"{fieldLabel} format is invalid";
                    break;
                case "required":
                    message = $"{fieldLabel} is required";
                    break;
            }

            return new { value, message };
        }
    }
}