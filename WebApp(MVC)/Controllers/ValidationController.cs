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

                // Improve label extraction for the field

                string fieldLabel = fieldName; // Default to fieldName

                // First try to find explicit label from TextField

                var textFieldLabelPattern = new Regex($@"<TextField[^>]*name\s*=\s*[""']{fieldName}[""'][^>]*label\s*=\s*[""']([^""']*)[""']");

                var labelMatch1 = textFieldLabelPattern.Match(jsxCode);

                if (labelMatch1.Success)

                {

                    fieldLabel = labelMatch1.Groups[1].Value;

                }

                else

                {

                    // Alternative TextField pattern (label comes first)

                    var altTextFieldLabelPattern = new Regex($@"<TextField[^>]*label\s*=\s*[""']([^""']*)[""'][^>]*name\s*=\s*[""']{fieldName}[""']");

                    var labelMatch2 = altTextFieldLabelPattern.Match(jsxCode);

                    if (labelMatch2.Success)

                    {

                        fieldLabel = labelMatch2.Groups[1].Value;

                    }

                    else

                    {

                        // Try to detect labels from the error messages or validation logic

                        var errorMessagePattern = new Regex($@"(?:new)?Errors\.{fieldName}\s*=\s*['""]([^'""]*?)\s(?:is required|must be|cannot|format)['""]");

                        var errorMatch = errorMessagePattern.Match(jsxCode);

                        if (errorMatch.Success)

                        {

                            fieldLabel = errorMatch.Groups[1].Value;

                        }

                        else

                        {

                            // Capitalize first letter as fallback

                            fieldLabel = char.ToUpper(fieldName[0]) + fieldName.Substring(1);

                        }

                    }

                }

                // First, extract error messages for this field from the React code

                var errorMessages = new Dictionary<string, string>();

                // Required validation error message

                var requiredErrorPattern = new Regex($@"if\s*\(\s*!(?:formData|credentials|values|formValues)\.{fieldName}\s*\)\s*{{\s*(?:new)?Errors\.?{fieldName}\s*=\s*['""]([^'""]*)['""]");

                var requiredMatch = requiredErrorPattern.Match(jsxCode);

                if (requiredMatch.Success)

                {

                    errorMessages["required"] = requiredMatch.Groups[1].Value;

                }

                // MinLength validation error message

                var minLengthErrorPattern = new Regex($@"if\s*\(\s*(?:formData|credentials|values|formValues)\.{fieldName}\.length\s*<\s*(\d+)\s*\)\s*{{\s*(?:new)?Errors\.?{fieldName}\s*=\s*['""]([^'""]*)['""]");

                var minLengthMatch = minLengthErrorPattern.Match(jsxCode);

                if (minLengthMatch.Success)

                {

                    errorMessages["minLength"] = minLengthMatch.Groups[2].Value;

                }

                // MaxLength validation error message

                var maxLengthErrorPattern = new Regex($@"if\s*\(\s*(?:formData|credentials|values|formValues)\.{fieldName}\.length\s*>\s*(\d+)\s*\)\s*{{\s*(?:new)?Errors\.?{fieldName}\s*=\s*['""]([^'""]*)['""]");

                var maxLengthMatch = maxLengthErrorPattern.Match(jsxCode);

                if (maxLengthMatch.Success)

                {

                    errorMessages["maxLength"] = maxLengthMatch.Groups[2].Value;

                }

                // Email validation error message

                var emailErrorPattern = new Regex($@"!(?:formData|credentials|values|formValues)\.{fieldName}\.match\(.*?email.*?\).*?(?:new)?Errors\.?{fieldName}\s*=\s*['""]([^'""]*)['""]");

                var emailMatch = emailErrorPattern.Match(jsxCode);

                if (emailMatch.Success)

                {

                    errorMessages["email"] = emailMatch.Groups[1].Value;

                }

                // Password validation error message

                var passwordErrorPattern = new Regex($@"!(?:formData|credentials|values|formValues)\.{fieldName}\.match\(.*?password.*?\).*?(?:new)?Errors\.?{fieldName}\s*=\s*['""]([^'""]*)['""]");

                var passwordMatch = passwordErrorPattern.Match(jsxCode);

                if (passwordMatch.Success)

                {

                    errorMessages["password"] = passwordMatch.Groups[1].Value;

                }

                // Pattern validation error message

                var patternErrorPattern = new Regex($@"!(?:formData|credentials|values|formValues)\.{fieldName}\.match\(.*?\).*?(?:new)?Errors\.?{fieldName}\s*=\s*['""]([^'""]*)['""]");

                var patternMatch = patternErrorPattern.Match(jsxCode);

                if (patternMatch.Success && !errorMessages.ContainsKey("email") && !errorMessages.ContainsKey("password"))

                {

                    errorMessages["pattern"] = patternMatch.Groups[1].Value;

                }

                // Now process the validation rules using the extracted error messages

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

                            // Prepare validation info with values and custom message from React code

                            object value = true;

                            string message = null;

                            // Extract value for specific validation types

                            if (pattern.Key == "minLength" || pattern.Key == "maxLength")

                            {

                                value = int.Parse(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value);

                            }

                            else if (pattern.Key == "pattern")

                            {

                                value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;

                            }

                            // Use the extracted error message if available

                            if (errorMessages.ContainsKey(pattern.Key))

                            {

                                message = errorMessages[pattern.Key];

                            }

                            else

                            {

                                // Fallback to default messages WITH CORRECT FIELD LABEL

                                message = pattern.Key switch

                                {

                                    "minLength" => $"{fieldLabel} must be at least {value} characters",

                                    "maxLength" => $"{fieldLabel} cannot exceed {value} characters",

                                    "email" => $"{fieldLabel} must be a valid email address",

                                    "password" => $"{fieldLabel} must be a valid password",

                                    "pattern" => $"{fieldLabel} format is invalid",

                                    "required" => $"{fieldLabel} is required",

                                    _ => $"{fieldLabel} validation failed"

                                };

                            }

                            if (!fieldRules.ContainsKey(pattern.Key))

                            {

                                fieldRules[pattern.Key] = new { value, message };

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

        private object ExtractValidationInfo(string validationType, Match match, string fieldLabel, string jsxCode)

        {

            object value = true;

            string message = $"{fieldLabel} validation failed"; // Default fallback

            // Extract value for min/max length and pattern validations

            if (validationType == "minLength")

            {

                value = int.Parse(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value);

                // Try to find the error message in the React code

                var messagePattern = new Regex($@"if\s*\(\s*(?:formData|credentials|values|formValues)\.(\w+)\.length\s*<\s*{value}\s*\)\s*{{\s*(?:new)?Errors\.?\1\s*=\s*['""]([^'""]*)['""]");

                var messageMatch = messagePattern.Match(jsxCode);

                if (messageMatch.Success && messageMatch.Groups[2].Success)

                {

                    message = messageMatch.Groups[2].Value;

                }

                else

                {

                    // Fallback message

                    message = $"{fieldLabel} must be at least {value} characters";

                }

            }

            else if (validationType == "maxLength")

            {

                value = int.Parse(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value);

                // Try to find the error message in the React code

                var messagePattern = new Regex($@"if\s*\(\s*(?:formData|credentials|values|formValues)\.(\w+)\.length\s*>\s*{value}\s*\)\s*{{\s*(?:new)?Errors\.?\1\s*=\s*['""]([^'""]*)['""]");

                var messageMatch = messagePattern.Match(jsxCode);

                if (messageMatch.Success && messageMatch.Groups[2].Success)

                {

                    message = messageMatch.Groups[2].Value;

                }

                else

                {

                    // Fallback message

                    message = $"{fieldLabel} cannot exceed {value} characters";

                }

            }

            else if (validationType == "required")

            {

                // Try to find the required error message in the React code

                var fieldNameLower = fieldLabel.ToLower().Replace(" ", "");

                var messagePattern = new Regex($@"if\s*\(\s*!(?:formData|credentials|values|formValues)\.(\w+)\s*\)\s*{{\s*(?:new)?Errors\.?\1\s*=\s*['""]([^'""]*)['""]");

                var messageMatch = messagePattern.Match(jsxCode);

                if (messageMatch.Success && messageMatch.Groups[2].Success)

                {

                    message = messageMatch.Groups[2].Value;

                }

                else

                {

                    // Fallback message

                    message = $"{fieldLabel} is required";

                }

            }

            else if (validationType == "email")

            {

                // Try to find the email error message in the React code

                var messagePattern = new Regex(@"!(?:formData|credentials|values|formValues)\.(\w+)\.match\(.*?email.*?\).*?(?:new)?Errors\.?\1\s*=\s*['""]([^'""]*)['""]");

                var messageMatch = messagePattern.Match(jsxCode);

                if (messageMatch.Success && messageMatch.Groups[2].Success)

                {

                    message = messageMatch.Groups[2].Value;

                }

                else

                {

                    // Fallback message

                    message = $"{fieldLabel} must be a valid email address";

                }

            }

            else if (validationType == "password")

            {

                // Try to find the password error message in the React code

                var messagePattern = new Regex(@"!(?:formData|credentials|values|formValues)\.(\w+)\.match\(.*?password.*?\).*?(?:new)?Errors\.?\1\s*=\s*['""]([^'""]*)['""]");

                var messageMatch = messagePattern.Match(jsxCode);

                if (messageMatch.Success && messageMatch.Groups[2].Success)

                {

                    message = messageMatch.Groups[2].Value;

                }

                else

                {

                    // Fallback message

                    message = $"{fieldLabel} must be a valid password";

                }

            }

            else if (validationType == "pattern")

            {

                value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;

                // Try to find the pattern error message in the React code

                var messagePattern = new Regex(@"!(?:formData|credentials|values|formValues)\.(\w+)\.match\(.*?\).*?(?:new)?Errors\.?\1\s*=\s*['""]([^'""]*)['""]");

                var messageMatch = messagePattern.Match(jsxCode);

                if (messageMatch.Success && messageMatch.Groups[2].Success)

                {

                    message = messageMatch.Groups[2].Value;

                }

                else

                {

                    // Fallback message

                    message = $"{fieldLabel} format is invalid";

                }

            }

            return new { value, message };

        }







    }


}
