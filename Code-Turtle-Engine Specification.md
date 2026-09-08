# **Code-Turtle-Engine: Autonomous Architectural Review Council**

**Lead Architect:** Prasad Sudhir Rane  
**Technology Stack:** C\#, .NET Core, ASP.NET Core Web API, Microsoft CodeAnalysis (Roslyn), Semantic Kernel, AWS ECS Fargate, Docker, GitHub Actions

## **1\. Executive Summary & Product Vision**

The Code-Turtle-Engine is an enterprise-grade AI pull request reviewer designed to transcend generic text generation. Current AI coding assistants lack full-repository context and frequently propose hallucinatory API calls because they treat code as plain text. Engineering teams spend excessive time reviewing basic syntax, unawaited tasks, and LINQ allocations rather than focusing on high-level system design.  
This project introduces a multi-agent .NET platform that grounds AI reasoning in deterministic compiler truths. By utilizing the Roslyn Compiler Platform to extract an Abstract Syntax Tree (AST), the system validates code structurally before passing it to a deliberative council of AI agents. The "Turtle Shell" acts as a protective, deterministic gatekeeper, ensuring zero-hallucination code reviews and strict enforcement of idiomatic C\# standards.

## **2\. Core Architectural Subsystems**

The engine is divided into three primary subsystems that operate in a Directed Acyclic Graph (DAG) flow: Ingestion, Deliberation, and Distribution.

| Subsystem | Primary Responsibility | Technical Implementation   |
| :---- | :---- | :---- |
| **The Turtle Shell (Gatekeeper)** | Deterministic fact-extraction and anti-hallucination layer. Parses PR diffs into syntax trees. | Microsoft.CodeAnalysis.CSharp (Roslyn Workspace API) to resolve symbols and map call graphs. |
| **The Review Council** | Parallel multi-agent deliberation on validated AST data, focusing on different engineering concerns. | SemanticKernel and Microsoft.Extensions.AI orchestrating distinct agent personas. |
| **Synthesizer & MCP Server** | Consolidation of agent feedback and exposure of capabilities to external IDEs. | Octokit for GitHub Actions integration and ModelContextProtocol.NET for local client access. |

## **3\. Subsystem Deep Dive: The Roslyn Gatekeeper**

The Gatekeeper prevents the LLM from analyzing raw, unstructured diffs. Instead, we implement a custom CSharpSyntaxWalker to traverse the incoming pull request files.

> * **Syntax Walking:** Extracts specific nodes such as MethodDeclarationSyntax and InvocationExpressionSyntax.  
> * **Semantic Modeling:** Uses the Roslyn compilation model to detect missing ConfigureAwait(false) statements, hidden boxing operations, and potentially dangerous type casting.  
> * **Payload Generation:** Formats the extracted semantic facts into a minified JSON payload. This preserves context window tokens and provides the AI with highly structured data rather than raw source code strings.

// Example: Conceptual Roslyn Extraction Payload for the LLM  
{  
  "Method": "ProcessPaymentAsync",  
  "Allocations": \["LINQ Closure (Line 42)", "Implicit Boxing (Line 45)"\],  
  "AsyncHealth": "Missing ConfigureAwait(false) on IHttpClientFactory invocation",  
  "Dependencies": \["IPaymentGateway", "ILogger"\]  
}

## **4\. Multi-Agent Orchestration via Semantic Kernel**

Once the Roslyn extraction is complete, the structured data is passed to a panel of parallel agents. Each agent acts with a specific operational directive, mimicking a real-world senior engineering council.

> * **The Allocations & Performance Expert:** Scrutinizes the Roslyn JSON payload for memory leaks, large object heap (LOH) risks, unawaited tasks, and thread safety issues in concurrent collections.  
> * **The Security Auditor:** Evaluates incoming RESTful API controllers and GraphQL endpoints for input sanitization, potential SQL injection vectors (e.g., raw string concatenation bypassing EF Core parameters), and authentication bypass risks.  
> * **The Idiomatic Architect:** Enforces modern C\# language features, clean architecture boundaries, and proper Dependency Injection lifecycles (e.g., capturing Transient services inside Singleton classes).  
> * **The Arbiter:** The final synthesis node. It evaluates the council's verdicts, discards overlapping nitpicks, resolves conflicts (e.g., performance vs. readability), and produces a unified, markdown-formatted PR review.

## **5\. Infrastructure, CI/CD, and Deployment Strategy**

To ensure enterprise readiness and high availability, Code-Turtle-Engine will be deployed using modern cloud infrastructure and DevOps practices.

> 1. **Containerization:** The ASP.NET Core Web API and background workers will be containerized using Docker, ensuring environment consistency between local development and production.  
> 2. **Cloud Compute:** The Docker images will be deployed to AWS ECS Fargate, providing serverless, event-driven compute capabilities capable of scaling instantly in response to GitHub webhook spikes.  
> 3. **Infrastructure as Code:** AWS resources (ECR, ECS clusters, IAM roles for Bedrock/LLM access) will be provisioned using Terraform.  
> 4. **CI/CD Pipelines:** GitHub Actions will automate the testing of the Roslyn parser, build the Docker images, and push updates to the AWS ECS cluster continuously.

## **6\. Key Success Metrics & Milestones**

The project's success is governed by strict deterministic and performance standards:

> * **Absolute Zero Hallucinations:** The system must never suggest a method, property, or architectural pattern that does not exist within the parsed AST or the target .NET framework.  
> * **Latency Constraint:** The complete end-to-end pipeline—from webhook ingestion to Octokit comment posting—must execute in under 60 seconds.  
> * **Extensibility:** The engine must successfully expose at least two Model Context Protocol (MCP) tool endpoints, allowing external agents (like Claude Code) to query the C\# abstract syntax tree dynamically.