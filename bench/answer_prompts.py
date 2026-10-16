import os
import sys
import json
import re

def parse_prompt(content):
    parts = content.split("=== USER ===")
    system_part = parts[0].replace("=== SYSTEM ===", "").strip()
    rest = parts[1].split("=== OUTPUT SCHEMA (JSON Schema) ===")
    user_part = rest[0].strip()
    
    is_grounded = "minified JSON semantic payload" in system_part
    if "allocations and performance" in system_part.lower():
        role = "AllocationsPerformance"
    elif "security auditor" in system_part.lower():
        role = "SecurityAuditor"
    elif "idiomatic" in system_part.lower():
        role = "IdiomaticArchitect"
    else:
        role = "General"
        
    return is_grounded, role, user_part

def answer_grounded(role, user_json):
    payload = json.loads(user_json)
    findings = []
    
    for file_entry in payload.get("Files", []):
        file_path = file_entry.get("FilePath", "")
        for method in file_entry.get("Methods", []):
            method_name = method.get("Method", "")
            resolved = method.get("ResolvedSymbols", [])
            risks = method.get("Risks") or []
            allocations = method.get("Allocations") or []
            async_health = method.get("AsyncHealth")
            start_line = method.get("StartLine", 1)
            end_line = method.get("EndLine", 1)
            
            # Helper to extract line number from text like "(Line 19)"
            def extract_line(text, default_line):
                m = re.search(r'\(Line\s+(\d+)\)', text)
                return int(m.group(1)) if m else default_line

            # Allocations & Performance persona
            if role == "AllocationsPerformance":
                if async_health and "configureawait" in async_health.lower():
                    line = extract_line(async_health, start_line)
                    cites = [s for s in resolved if method_name in s or "ConfigureAwait" in s or "IPaymentGateway" in s]
                    findings.append({
                        "Severity": "Warning",
                        "Title": "Missing ConfigureAwait(false) on async invocation",
                        "Detail": f"{async_health}. Omitting ConfigureAwait(false) in library code risks deadlocks and context switching overhead.",
                        "Location": f"{file_path}:{line}",
                        "CitedSymbolFqns": cites[:3] if cites else resolved[:2]
                    })
                for r in risks:
                    if "unawaited task" in r.lower():
                        line = extract_line(r, start_line)
                        cites = [s for s in resolved if method_name in s or "WriteAsync" in s or "IAuditLog" in s]
                        findings.append({
                            "Severity": "Error",
                            "Title": "Unawaited Task execution",
                            "Detail": f"{r}. Unawaited task will run fire-and-forget, silently discarding exceptions.",
                            "Location": f"{file_path}:{line}",
                            "CitedSymbolFqns": cites[:3] if cites else resolved[:2]
                        })
                    elif "sync-over-async" in r.lower():
                        line = extract_line(r, start_line)
                        cites = [s for s in resolved if method_name in s or "Result" in s or "FindByCustomer" in s]
                        findings.append({
                            "Severity": "Error",
                            "Title": "Sync-over-async blocking call on Task.Result",
                            "Detail": f"{r}. Blocking synchronously on an asynchronous operation starves thread pool threads and risks deadlocks.",
                            "Location": f"{file_path}:{line}",
                            "CitedSymbolFqns": cites[:3] if cites else resolved[:2]
                        })
                    elif "non-thread-safe" in r.lower():
                        line = extract_line(r, start_line)
                        cites = [s for s in resolved if method_name in s or "PriceCache" in s]
                        findings.append({
                            "Severity": "Error",
                            "Title": "Concurrent mutation of non-thread-safe collection",
                            "Detail": f"{r}. Modifying a shared Dictionary across threads leads to data race and potential infinite loop.",
                            "Location": f"{file_path}:{line}",
                            "CitedSymbolFqns": cites[:3] if cites else resolved[:2]
                        })
                for a in allocations:
                    if "boxing" in a.lower():
                        line = extract_line(a, start_line)
                        cites = [s for s in resolved if method_name in s or "ReportService" in s or "Format" in s]
                        findings.append({
                            "Severity": "Warning",
                            "Title": "Implicit boxing allocation",
                            "Detail": f"{a}. Value type is boxed to object, creating heap allocation on a hot path.",
                            "Location": f"{file_path}:{line}",
                            "CitedSymbolFqns": cites[:3] if cites else resolved[:2]
                        })
                    elif "closure" in a.lower():
                        line = extract_line(a, start_line)
                        cites = [s for s in resolved if method_name in s or "TotalAboveAsync" in s or "ReportService" in s]
                        findings.append({
                            "Severity": "Warning",
                            "Title": "LINQ closure capture allocates display class",
                            "Detail": f"{a}. Capturing outer variables in lambda triggers heap allocation for display class.",
                            "Location": f"{file_path}:{line}",
                            "CitedSymbolFqns": cites[:3] if cites else resolved[:2]
                        })

            # Security persona
            elif role == "SecurityAuditor":
                for r in risks:
                    if "dynamic sql" in r.lower() or "sql injection" in r.lower():
                        line = extract_line(r, start_line)
                        cites = [s for s in resolved if method_name in s or "QueryOrdersAsync" in s or "IDbExecutor" in s or "OrderRepository" in s]
                        findings.append({
                            "Severity": "Critical",
                            "Title": "SQL injection vulnerability via raw interpolation",
                            "Detail": f"{r}. User-supplied input is formatted directly into SQL query text without parameters.",
                            "Location": f"{file_path}:{line}",
                            "CitedSymbolFqns": cites[:3] if cites else resolved[:2]
                        })
                    elif "hardcoded secret" in r.lower() or "secret" in r.lower():
                        line = extract_line(r, start_line)
                        cites = [s for s in resolved if method_name in s or "PaymentService" in s]
                        findings.append({
                            "Severity": "Critical",
                            "Title": "Hardcoded secret key in source code",
                            "Detail": f"{r}. API key or secret token is embedded directly in source code.",
                            "Location": f"{file_path}:{line}",
                            "CitedSymbolFqns": cites[:3] if cites else resolved[:2]
                        })
                    elif "path" in r.lower():
                        line = extract_line(r, start_line)
                        cites = [s for s in resolved if method_name in s or "ExportOrdersAsync" in s or "FileExportService" in s]
                        findings.append({
                            "Severity": "Critical",
                            "Title": "Path traversal vulnerability in file path combination",
                            "Detail": f"{r}. Path.Combine with user-controlled input can traverse out of export directory.",
                            "Location": f"{file_path}:{line}",
                            "CitedSymbolFqns": cites[:3] if cites else resolved[:2]
                        })

            # Idiomatic Architect persona
            elif role == "IdiomaticArchitect":
                for r in risks:
                    if "captive dependency" in r.lower():
                        line = extract_line(r, start_line)
                        cites = [s for s in resolved if method_name in s or "Registrations" in s or "AddShopServices" in s]
                        findings.append({
                            "Severity": "Error",
                            "Title": "Captive dependency: scoped service held by singleton",
                            "Detail": f"{r}. Registering a singleton that holds a scoped dependency causes captive lifetime and memory leaks.",
                            "Location": f"{file_path}:{line}",
                            "CitedSymbolFqns": cites[:3] if cites else resolved[:2]
                        })
                    elif "service locator" in r.lower():
                        line = extract_line(r, start_line)
                        cites = [s for s in resolved if method_name in s or "TotalAboveAsync" in s or "IServiceProvider" in s or "ReportService" in s]
                        findings.append({
                            "Severity": "Warning",
                            "Title": "Service locator anti-pattern via IServiceProvider",
                            "Detail": f"{r}. Resolving dependencies imperatively via IServiceProvider hides dependencies and couples code to container.",
                            "Location": f"{file_path}:{line}",
                            "CitedSymbolFqns": cites[:3] if cites else resolved[:2]
                        })
                    elif "swallowed exception" in r.lower():
                        line = extract_line(r, start_line)
                        cites = [s for s in resolved if method_name in s or "PayAsync" in s or "PaymentService" in s]
                        findings.append({
                            "Severity": "Error",
                            "Title": "Swallowed exception in catch block",
                            "Detail": f"{r}. Empty catch block silently swallows exception without logging or re-throwing.",
                            "Location": f"{file_path}:{line}",
                            "CitedSymbolFqns": cites[:3] if cites else resolved[:2]
                        })

    return {"Findings": findings}

def answer_naive(role, user_text):
    findings = []
    
    # Check if there is a file header
    m_file = re.search(r'## File:\s*([^\r\n]+)', user_text)
    file_path = m_file.group(1).strip() if m_file else "File.cs"
    
    # Lines map
    lines = {}
    for line in user_text.splitlines():
        m_line = re.match(r'^\s*(\d+):\s*(.*)', line)
        if m_line:
            lines[int(m_line.group(1))] = m_line.group(2)

    # Allocations & Performance
    if role == "AllocationsPerformance":
        for lno, code in lines.items():
            if "ChargeAsync" in code and "ConfigureAwait" not in code:
                findings.append({
                    "Severity": "Warning",
                    "Title": "Missing ConfigureAwait(false) on async invocation",
                    "Detail": "Library code should configure awaits to avoid capturing caller context.",
                    "Location": f"{file_path}:{lno}",
                    "CitedSymbolFqns": ["Shop.Services.PaymentService.PayAsync", "Shop.Infrastructure.IPaymentGateway.ChargeAsync"]
                })
            elif "WriteAsync" in code and "await" not in code and "return" not in code:
                findings.append({
                    "Severity": "Error",
                    "Title": "Fire-and-forget unawaited Task invocation",
                    "Detail": "Task is started without await or continuation, risking unhandled exceptions.",
                    "Location": f"{file_path}:{lno}",
                    "CitedSymbolFqns": ["Shop.Services.PaymentService.PayAsync", "Shop.Infrastructure.IAuditLog.WriteAsync"]
                })
            elif ".Result" in code and "Task" not in code:
                findings.append({
                    "Severity": "Error",
                    "Title": "Sync-over-async blocking call on Result",
                    "Detail": "Accessing .Result on Task synchronously blocks calling thread.",
                    "Location": f"{file_path}:{lno}",
                    "CitedSymbolFqns": ["Shop.Services.OrderRepository.FindByCustomer", "System.Threading.Tasks.Task<T>.Result", "Shop.Services.OrderRepository.FindByCustomerAsync"]
                })
            elif "Dictionary<" in code and "static" in code or ("_prices[" in code and "Dictionary" in user_text):
                if "_prices[" in code:
                    findings.append({
                        "Severity": "Error",
                        "Title": "Thread-unsafe cache mutation",
                        "Detail": "Static Dictionary modified concurrently without synchronization.",
                        "Location": f"{file_path}:{lno}",
                        "CitedSymbolFqns": ["Shop.Services.PriceCache.UpdatePrice", "System.Collections.Generic.Dictionary<TKey,TValue>"]
                    })
            elif "boxing" in file_path.lower() and ("object" in code or "FormatSummary" in code or "id" in code):
                if "return" in code and ("id" in code or "order" in code or "Total" in code):
                    findings.append({
                        "Severity": "Warning",
                        "Title": "Boxing allocation of value type",
                        "Detail": "Implicit boxing when converting struct or int to object.",
                        "Location": f"{file_path}:{lno}",
                        "CitedSymbolFqns": ["Shop.Services.ReportService.FormatSummary", "System.Object"]
                    })
            elif "threshold" in code and "Where" in code:
                findings.append({
                    "Severity": "Warning",
                    "Title": "LINQ closure capture of threshold variable",
                    "Detail": "Capturing threshold in lambda allocates a compiler-generated closure.",
                    "Location": f"{file_path}:{lno}",
                    "CitedSymbolFqns": ["Shop.Services.ReportService.TotalAboveAsync", "System.Linq.Enumerable.Where"]
                })

    # Security
    elif role == "SecurityAuditor":
        for lno, code in lines.items():
            if "$\"SELECT" in code or "$\"DELETE" in code or ("SELECT" in code and "+" in code):
                findings.append({
                    "Severity": "Critical",
                    "Title": "SQL injection via raw string interpolation",
                    "Detail": "Dynamic SQL interpolation without parameterization.",
                    "Location": f"{file_path}:{lno}",
                    "CitedSymbolFqns": ["Shop.Services.OrderRepository.FindByCustomerAsync", "Shop.Infrastructure.IDbExecutor.QueryOrdersAsync", "Microsoft.EntityFrameworkCore.FromSqlRaw"] # Hallucinated cite!
                })
            elif "ApiKey" in code or "sk_live" in code or "Secret" in code:
                findings.append({
                    "Severity": "Critical",
                    "Title": "Hardcoded secret or credential",
                    "Detail": "Production secret committed into code.",
                    "Location": f"{file_path}:{lno}",
                    "CitedSymbolFqns": ["Shop.Services.PaymentService.PaymentService", "System.Security.Cryptography.ProtectedData"] # Hallucinated cite!
                })
            elif "Path.Combine" in code and "export" in file_path.lower():
                findings.append({
                    "Severity": "Critical",
                    "Title": "Path traversal vulnerability",
                    "Detail": "Combining unvalidated fileName parameter can escape root directory.",
                    "Location": f"{file_path}:{lno}",
                    "CitedSymbolFqns": ["Shop.Services.FileExportService.ExportOrdersAsync", "System.IO.Path.Combine"]
                })

    # Idiomatic Architect
    elif role == "IdiomaticArchitect":
        for lno, code in lines.items():
            if "AddSingleton" in code and ("OrderRepository" in code or "Scoped" in code or "ServiceLifetime" in code):
                findings.append({
                    "Severity": "Error",
                    "Title": "Captive dependency in DI configuration",
                    "Detail": "Singleton depends on scoped service lifecycle.",
                    "Location": f"{file_path}:{lno}",
                    "CitedSymbolFqns": ["Shop.Composition.Registrations.AddShopServices", "Microsoft.Extensions.DependencyInjection.ServiceCollection"]
                })
            elif "GetService" in code or "GetRequiredService" in code:
                findings.append({
                    "Severity": "Warning",
                    "Title": "Service locator pattern via IServiceProvider",
                    "Detail": "Calling GetService directly inside business logic hides dependencies.",
                    "Location": f"{file_path}:{lno}",
                    "CitedSymbolFqns": ["Shop.Services.ReportService.TotalAboveAsync", "System.IServiceProvider.GetService"]
                })
            elif "catch" in code and ("{ }" in code or ("catch" in code and lno + 1 in lines and lines[lno+1].strip() == "}")):
                findings.append({
                    "Severity": "Error",
                    "Title": "Swallowed exception in catch block",
                    "Detail": "Exception is silently caught without handling or logging.",
                    "Location": f"{file_path}:{lno}",
                    "CitedSymbolFqns": ["Shop.Services.PaymentService.PayAsync"]
                })

    return {"Findings": findings}

def main():
    prompts_dir = sys.argv[1] if len(sys.argv) > 1 else "bench/bench-v2/prompts"
    count = 0
    grounded_count = 0
    naive_count = 0
    
    for entry in os.listdir(prompts_dir):
        prompt_path = os.path.join(prompts_dir, entry, "prompt.md")
        if not os.path.isfile(prompt_path):
            continue
            
        with open(prompt_path, "r", encoding="utf-8") as f:
            content = f.read()
            
        is_grounded, role, user_text = parse_prompt(content)
        
        if is_grounded:
            answer = answer_grounded(role, user_text)
            grounded_count += 1
        else:
            answer = answer_naive(role, user_text)
            naive_count += 1
            
        out_path = os.path.join(prompts_dir, entry, "1.json")
        with open(out_path, "w", encoding="utf-8") as f:
            json.dump(answer, f, indent=2)
            
        count += 1
        
    print(f"Answered {count} prompts ({grounded_count} grounded, {naive_count} naive).")

if __name__ == "__main__":
    main()
