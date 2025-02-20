using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;

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
                string jsxCode = request?.JsxCode;

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

                // Basic React code validation
                var validationResult = ValidateReactCode(jsxCode);
                if (!validationResult.IsValid)
                {
                    return BadRequest(new
                    {
                        message = "Invalid React Code",
                        errors = validationResult.Errors
                    });
                }

                // Extract validation rules
                var validationRules = ExtractValidationRules(jsxCode);

                var result = new Dictionary<string, object>
                {
                    ["validationRules"] = validationRules,
                    ["metadata"] = new
                    {
                        totalFields = validationRules.Count,
                        //timestamp = DateTime.UtcNow
                    }
                };

                string jsonResult = JsonConvert.SerializeObject(result, Formatting.Indented);
                return Ok(jsonResult);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error processing the form: {ex.Message}");
            }
        }

        private class ValidationResult
        {
            public bool IsValid { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
        }

        private ValidationResult ValidateReactCode(string code)
        {
            var result = new ValidationResult { IsValid = true };

            try
            {
                // Check for basic React component structure
                if (!IsValidReactComponent(code))
                {
                    result.IsValid = false;
                    result.Errors.Add("Invalid React component structure");
                    return result;
                }

                //// Check for balanced JSX tags
                //if (!HasBalancedTags(code))
                //{
                //    result.IsValid = false;
                //    result.Errors.Add("Unmatched JSX tags found");
                //}

                return result;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Validation error: {ex.Message}");
                return result;
            }
        }

        private bool IsValidReactComponent(string code)
        {
            // Check for either functional or class component
            var functionComponent = new Regex(@"(function|const)\s+\w+\s*\([^)]*\)\s*{");
            var classComponent = new Regex(@"class\s+\w+\s+extends\s+React\.Component");
            var arrowFunction = new Regex(@"const\s+\w+\s*=\s*\([^)]*\)\s*=>");

            return functionComponent.IsMatch(code) ||
                   classComponent.IsMatch(code) ||
                   arrowFunction.IsMatch(code);
        }

        private bool HasBalancedTags(string code)
        {
            var stack = new Stack<string>();
            var tagPattern = new Regex(@"</?([a-zA-Z][a-zA-Z0-9]*)|/>", RegexOptions.Compiled);

            foreach (Match match in tagPattern.Matches(code))
            {
                if (match.Value == "/>")
                {
                    continue; // Self-closing tag
                }

                if (match.Value.StartsWith("</"))
                {
                    if (stack.Count == 0 || stack.Pop() != match.Groups[1].Value)
                    {
                        return false;
                    }
                }
                else if (match.Value.StartsWith("<"))
                {
                    stack.Push(match.Groups[1].Value);
                }
            }

            return stack.Count == 0;
        }

        private Dictionary<string, object> ExtractValidationRules(string jsxCode)
        {
            var rules = new Dictionary<string, object>();

            // Required Field Validation
            var requiredPattern = new Regex(@"if\s*\(\s*!formData\.(\w+)\s*\)", RegexOptions.IgnoreCase);
            var requiredMatches = requiredPattern.Matches(jsxCode);

            foreach (Match match in requiredMatches)
            {
                var fieldName = match.Groups[1].Value;
                if (!rules.ContainsKey(fieldName))
                {
                    rules[fieldName] = new Dictionary<string, object>();
                }
                var fieldRules = (Dictionary<string, object>)rules[fieldName];
                fieldRules["required"] = new
                {
                    value = true,
                    message = $"{fieldName} is required"
                };
            }

            // Regex Validation (General)
            var regexPattern = new Regex(@"/(.+?)/.test\(formData\.(\w+)\)", RegexOptions.IgnoreCase);
            var regexMatches = regexPattern.Matches(jsxCode);

            foreach (Match match in regexMatches)
            {
                var fieldName = match.Groups[2].Value;
                if (!rules.ContainsKey(fieldName))
                {
                    rules[fieldName] = new Dictionary<string, object>();
                }
                var fieldRules = (Dictionary<string, object>)rules[fieldName];
                fieldRules["pattern"] = new
                {
                    value = match.Groups[1].Value,
                    message = $"{fieldName} format is invalid"
                };
            }

            // Email Validation
            var emailPattern = new Regex(@"if\s*\(\s*!formData\.(\w+)\s*\.match\(.+?\)\)", RegexOptions.IgnoreCase);
            var emailMatches = emailPattern.Matches(jsxCode);

            foreach (Match match in emailMatches)
            {
                var fieldName = match.Groups[1].Value;
                if (!rules.ContainsKey(fieldName))
                {
                    rules[fieldName] = new Dictionary<string, object>();
                }
                var fieldRules = (Dictionary<string, object>)rules[fieldName];
                fieldRules["email"] = new
                {
                    value = true,
                    message = $"{fieldName} must be a valid email address"
                };
            }

            // Phone Number Validation
            var phonePattern = new Regex(@"if\s*\(\s*!formData\.(\w+)\s*\.match\(.+?phone.+?\)\)", RegexOptions.IgnoreCase);
            var phoneMatches = phonePattern.Matches(jsxCode);

            foreach (Match match in phoneMatches)
            {
                var fieldName = match.Groups[1].Value;
                if (!rules.ContainsKey(fieldName))
                {
                    rules[fieldName] = new Dictionary<string, object>();
                }
                var fieldRules = (Dictionary<string, object>)rules[fieldName];
                fieldRules["phone"] = new
                {
                    value = true,
                    message = $"{fieldName} must be a valid phone number"
                };
            }

            // Password Validation (at least 8 characters, 1 uppercase, 1 number)
            var passwordPattern = new Regex(@"if\s*\(\s*!formData\.(\w+)\s*\.match\(.+?password.+?\)\)", RegexOptions.IgnoreCase);
            var passwordMatches = passwordPattern.Matches(jsxCode);

            foreach (Match match in passwordMatches)
            {
                var fieldName = match.Groups[1].Value;
                if (!rules.ContainsKey(fieldName))
                {
                    rules[fieldName] = new Dictionary<string, object>();
                }
                var fieldRules = (Dictionary<string, object>)rules[fieldName];
                fieldRules["password"] = new
                {
                    value = true,
                    message = $"{fieldName} must be a valid password (at least 8 characters, 1 uppercase letter, and 1 number)"
                };
            }

            return rules;
        }
    }

    }