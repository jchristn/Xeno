namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Xeno.Client;
    using Xeno.Client.Routing;
    using Xeno.Core.Models;

    /// <summary>
    /// Listener-level routing: local passthrough, streaming, headers, cancellation, fallback, merged listings, and logging.
    /// </summary>
    public static class HybridRoutingProxyTestSuites
    {
        #region Public-Methods

        /// <summary>
        /// The listener routes requests to local providers and the network.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor ListenerRoutingSuite()
        {
            const string suite = "HybridListenerRouting";
            return new TestSuiteDescriptor(suite, "Hybrid Listener Routing", new List<TestCaseDescriptor>
            {
                Case(suite, "LocalRouteProxiesOpenAiChatToOllama", "OpenAI chat for a local model goes straight to the local Ollama, not the tunnel", async () =>
                {
                    using (FakeProviderServer ollama = new FakeProviderServer(request => request.Path == "/v1/chat/completions" ? FakeProviderResponse.Json("{\"id\":\"chatcmpl-local\",\"object\":\"chat.completion\"}") : Tags("llama3.1:8b")))
                    using (ListenerRoutingHarness listener = new ListenerRoutingHarness(settings => settings.Providers.Add(ListenerRoutingHarness.Provider("local-ollama", "Ollama", ollama.BaseUrl, "llama3.1:8b"))))
                    using (HttpResponseMessage response = await PostAsync(listener, "/v1/chat/completions", "{\"model\":\"llama3.1:8b\",\"messages\":[{\"role\":\"user\",\"content\":\"hi\"}]}").ConfigureAwait(false))
                    {
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        Check((int)response.StatusCode == 200 && body.Contains("chatcmpl-local", StringComparison.Ordinal), "Local provider response was not returned: " + body);
                        Check(Header(response, "X-Xeno-Route") == "local:local-ollama", "Route header is wrong: " + Header(response, "X-Xeno-Route"));
                        Check(listener.TunnelRequests.IsEmpty, "A local request went through the tunnel.");
                        FakeProviderRequest forwarded = ollama.Requests.Single(item => item.Path == "/v1/chat/completions");
                        Check(forwarded.Body.Contains("\"content\":\"hi\"", StringComparison.Ordinal), "Body was not forwarded unchanged.");
                    }
                }),
                Case(suite, "LocalRouteStreamsOpenAiSse", "Server-sent events from a local provider stream through as they arrive", async () =>
                {
                    List<string> chunks = new List<string> { "data: {\"choices\":[{\"delta\":{\"content\":\"Hel\"}}]}\n\n", "data: {\"choices\":[{\"delta\":{\"content\":\"lo\"}}]}\n\n", "data: [DONE]\n\n" };
                    using (FakeProviderServer vllm = new FakeProviderServer(request => request.Path == "/v1/chat/completions" ? new FakeProviderResponse { ContentType = "text/event-stream", Chunks = chunks, ChunkDelayMs = 30 } : Models("mistral-small")))
                    using (ListenerRoutingHarness listener = new ListenerRoutingHarness(settings => settings.Providers.Add(ListenerRoutingHarness.Provider("vllm", "vLLM", vllm.BaseUrl, "mistral-small"))))
                    using (HttpResponseMessage response = await PostAsync(listener, "/v1/chat/completions", "{\"model\":\"mistral-small\",\"stream\":true,\"messages\":[]}").ConfigureAwait(false))
                    {
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        Check(response.Content.Headers.ContentType?.MediaType == "text/event-stream", "SSE content type was not preserved.");
                        Check(body == String.Concat(chunks), "Streamed body changed in transit: " + body);
                    }
                }),
                Case(suite, "LocalRouteStreamsOllamaNdjson", "Ollama NDJSON streams through unchanged", async () =>
                {
                    List<string> chunks = new List<string> { "{\"message\":{\"content\":\"a\"},\"done\":false}\n", "{\"message\":{\"content\":\"b\"},\"done\":true}\n" };
                    using (FakeProviderServer ollama = new FakeProviderServer(request => request.Path == "/api/chat" ? new FakeProviderResponse { ContentType = "application/x-ndjson", Chunks = chunks } : Tags("qwen3:latest")))
                    using (ListenerRoutingHarness listener = new ListenerRoutingHarness(settings => settings.Providers.Add(ListenerRoutingHarness.Provider("ollama", "Ollama", ollama.BaseUrl, "qwen3:latest"))))
                    using (HttpResponseMessage response = await PostAsync(listener, "/api/chat", "{\"model\":\"qwen3\",\"messages\":[]}").ConfigureAwait(false))
                    {
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        Check(body == String.Concat(chunks), "NDJSON body changed in transit: " + body);
                        Check(Header(response, "X-Xeno-Route") == "local:ollama", "Route header is wrong.");
                    }
                }),
                Case(suite, "LocalRoutePreservesStatusAndFiltersHeaders", "Provider status codes and content types pass through; unsafe headers do not", async () =>
                {
                    using (FakeProviderServer ollama = new FakeProviderServer(request =>
                    {
                        if (request.Path != "/v1/embeddings") return Tags("nomic-embed-text:latest");
                        FakeProviderResponse response = new FakeProviderResponse { StatusCode = 429, ContentType = "application/problem+json", Body = "{\"error\":\"busy\"}" };
                        response.Headers["Set-Cookie"] = "session=secret";
                        response.Headers["Access-Control-Allow-Origin"] = "https://evil.example";
                        response.Headers["X-Upstream-Trace"] = "trace-1";
                        response.Headers["X-Xeno-Route"] = "spoofed";
                        return response;
                    }))
                    using (ListenerRoutingHarness listener = new ListenerRoutingHarness(settings => settings.Providers.Add(ListenerRoutingHarness.Provider("ollama", "Ollama", ollama.BaseUrl, "nomic-embed-text:latest"))))
                    using (HttpResponseMessage response = await PostAsync(listener, "/v1/embeddings", "{\"model\":\"nomic-embed-text\",\"input\":\"x\"}").ConfigureAwait(false))
                    {
                        Check((int)response.StatusCode == 429, "Status code was not preserved.");
                        Check(response.Content.Headers.ContentType?.MediaType == "application/problem+json", "Content type was not preserved.");
                        Check(Header(response, "X-Upstream-Trace") == "trace-1", "A safe header was dropped.");
                        Check(!response.Headers.Contains("Set-Cookie"), "Set-Cookie leaked through.");
                        Check(Header(response, "Access-Control-Allow-Origin") != "https://evil.example", "Provider CORS header leaked through.");
                        Check(Header(response, "X-Xeno-Route") == "local:ollama", "Provider spoofed the route header.");
                    }
                }),
                Case(suite, "LocalRouteSendsProviderCredential", "The provider credential replaces the caller's Authorization header", async () =>
                {
                    using (FakeProviderServer cloud = new FakeProviderServer(request => request.Path == "/v1/chat/completions" ? FakeProviderResponse.Json("{}") : Models("gpt-oss:120b")))
                    using (ListenerRoutingHarness listener = new ListenerRoutingHarness(settings =>
                    {
                        ClientRunnerSettings provider = ListenerRoutingHarness.Provider("ollama-cloud", "OpenAICompatible", cloud.BaseUrl, "gpt-oss:120b");
                        provider.Credential = "sk-provider-secret";
                        provider.ShareWithNetwork = false;
                        settings.Providers.Add(provider);
                    }))
                    using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, listener.Url("/v1/chat/completions")))
                    {
                        request.Headers.TryAddWithoutValidation("Authorization", "Bearer caller-dummy-key");
                        request.Content = new StringContent("{\"model\":\"gpt-oss:120b\",\"messages\":[]}", Encoding.UTF8, "application/json");
                        using (HttpResponseMessage response = await listener.Http.SendAsync(request).ConfigureAwait(false))
                        {
                            Check((int)response.StatusCode == 200, "Local-only provider request failed.");
                        }

                        FakeProviderRequest forwarded = cloud.Requests.Single(item => item.Path == "/v1/chat/completions");
                        Check(forwarded.Headers.TryGetValue("Authorization", out string? auth) && auth == "Bearer sk-provider-secret", "Provider credential was not sent.");
                    }
                }),
                Case(suite, "LocalRoutePropagatesCancellation", "A caller that disconnects cancels the upstream provider request", async () =>
                {
                    List<string> chunks = Enumerable.Range(0, 50).Select(i => "data: {\"n\":" + i + "}\n\n").ToList();
                    using (FakeProviderServer vllm = new FakeProviderServer(request => request.Path == "/v1/chat/completions" ? new FakeProviderResponse { ContentType = "text/event-stream", Chunks = chunks, ChunkDelayMs = 100 } : Models("slow")))
                    using (ListenerRoutingHarness listener = new ListenerRoutingHarness(settings => settings.Providers.Add(ListenerRoutingHarness.Provider("vllm", "vLLM", vllm.BaseUrl, "slow"))))
                    {
                        using (CancellationTokenSource cancel = new CancellationTokenSource())
                        using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, listener.Url("/v1/chat/completions")) { Content = new StringContent("{\"model\":\"slow\",\"stream\":true,\"messages\":[]}", Encoding.UTF8, "application/json") })
                        using (HttpResponseMessage response = await listener.Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel.Token).ConfigureAwait(false))
                        using (Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        {
                            byte[] buffer = new byte[64];
                            await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                            cancel.Cancel();
                        }

                        bool aborted = await WaitAsync(() => vllm.AbortedStreams > 0, 8000).ConfigureAwait(false);
                        Check(aborted, "The provider kept streaming after the caller disconnected.");
                    }
                }),
                Case(suite, "FallbackUsesNetworkWhenEnabled", "A rule with fallback sends the request to the network, excluding this client, when the provider is down", async () =>
                {
                    using (ListenerRoutingHarness listener = new ListenerRoutingHarness(settings =>
                    {
                        settings.Providers.Add(ListenerRoutingHarness.Provider("ollama", "Ollama", "http://127.0.0.1:" + FakeProviderServer.GetFreePort(), "llama3.1:8b"));
                        settings.LocalInferenceServer.Routes.Add(new LocalInferenceRouteRule { Model = "llama3.1:8b", Target = LocalInferenceRouteTargetEnum.Local, Provider = "ollama", FallbackToNetwork = true });
                    }))
                    using (HttpResponseMessage response = await PostAsync(listener, "/v1/chat/completions", "{\"model\":\"llama3.1:8b\",\"messages\":[]}").ConfigureAwait(false))
                    {
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        Check(body.Contains("network", StringComparison.Ordinal), "Fallback did not return the network response: " + body);
                        Check(Header(response, "X-Xeno-Route") == "network" && Header(response, "X-Xeno-Fallback") == "true", "Fallback headers are wrong.");
                        ClientLocalInferenceRequestMessage tunneled = listener.TunnelRequests.Single();
                        Check(tunneled.ExcludeRequestingClient, "Fallback did not ask the server to exclude this client.");
                        Check(listener.Service.Backends.Get("ollama")?.Healthy == false, "The failed provider was not marked unhealthy.");
                    }
                }),
                Case(suite, "NoFallbackReturnsProviderShaped502", "Without a fallback rule a down provider returns a provider-shaped 502", async () =>
                {
                    using (ListenerRoutingHarness listener = new ListenerRoutingHarness(settings => settings.Providers.Add(ListenerRoutingHarness.Provider("ollama", "Ollama", "http://127.0.0.1:" + FakeProviderServer.GetFreePort(), "llama3.1:8b"))))
                    {
                        using (HttpResponseMessage openAi = await PostAsync(listener, "/v1/chat/completions", "{\"model\":\"llama3.1:8b\",\"messages\":[]}").ConfigureAwait(false))
                        {
                            string body = await openAi.Content.ReadAsStringAsync().ConfigureAwait(false);
                            Check((int)openAi.StatusCode == 502 && body.Contains("\"error\"", StringComparison.Ordinal) && body.Contains("local_provider_unavailable", StringComparison.Ordinal), "OpenAI-shaped 502 is wrong: " + body);
                        }

                        using (HttpResponseMessage ollamaShape = await PostAsync(listener, "/api/generate", "{\"model\":\"llama3.1:8b\",\"prompt\":\"x\"}").ConfigureAwait(false))
                        {
                            string body = await ollamaShape.Content.ReadAsStringAsync().ConfigureAwait(false);
                            Check((int)ollamaShape.StatusCode == 502 && body.Contains("\"error\"", StringComparison.Ordinal), "Ollama-shaped 502 is wrong: " + body);
                        }

                        Check(listener.TunnelRequests.IsEmpty, "A request without a fallback rule went to the network.");
                    }
                }),
                Case(suite, "NoFallbackAfterFirstByte", "A provider that fails mid-stream ends the stream with an error and never retries on the network", async () =>
                {
                    using (TruncatedStreamProviderServer vllm = new TruncatedStreamProviderServer("/v1/chat/completions", "data: {\"n\":1}\n\n", "flaky"))
                    using (ListenerRoutingHarness listener = new ListenerRoutingHarness(settings =>
                    {
                        settings.Providers.Add(ListenerRoutingHarness.Provider("vllm", "vLLM", vllm.BaseUrl, "flaky"));
                        settings.LocalInferenceServer.Routes.Add(new LocalInferenceRouteRule { Model = "flaky", Target = LocalInferenceRouteTargetEnum.Local, Provider = "vllm", FallbackToNetwork = true });
                    }))
                    using (HttpResponseMessage response = await PostAsync(listener, "/v1/chat/completions", "{\"model\":\"flaky\",\"stream\":true,\"messages\":[]}").ConfigureAwait(false))
                    {
                        string body = await ReadAllTolerantAsync(response).ConfigureAwait(false);
                        Check(body.StartsWith("data: {\"n\":1}", StringComparison.Ordinal), "The first chunk was not delivered: " + body);
                        Check(body.Contains("upstream_interrupted", StringComparison.Ordinal), "The stream did not end with an error event: " + body);
                        Check(listener.TunnelRequests.IsEmpty, "A started stream was retried on the network.");
                        Check(vllm.StreamRequests == 1, "The provider was called more than once.");
                    }
                }),
                Case(suite, "TunnelDownDoesNotBreakLocalRoutes", "Local models keep working with no websocket tunnel; network models get a 502", async () =>
                {
                    using (FakeProviderServer ollama = new FakeProviderServer(request => request.Path == "/api/generate" ? FakeProviderResponse.Json("{\"response\":\"ok\",\"done\":true}") : Tags("llama3.1:8b")))
                    using (ListenerRoutingHarness listener = new ListenerRoutingHarness(settings => settings.Providers.Add(ListenerRoutingHarness.Provider("ollama", "Ollama", ollama.BaseUrl, "llama3.1:8b")), tunnel: false))
                    {
                        using (HttpResponseMessage local = await PostAsync(listener, "/api/generate", "{\"model\":\"llama3.1:8b\",\"prompt\":\"x\",\"stream\":false}").ConfigureAwait(false))
                        {
                            Check((int)local.StatusCode == 200, "Local route failed without a tunnel.");
                        }

                        using (HttpResponseMessage network = await PostAsync(listener, "/api/generate", "{\"model\":\"deepseek-r1:671b\",\"prompt\":\"x\",\"stream\":false}").ConfigureAwait(false))
                        {
                            Check((int)network.StatusCode == 502, "Network route without a tunnel was not a 502.");
                        }
                    }
                }),
                Case(suite, "LocalDownDoesNotBreakNetworkRoutes", "A dead local provider does not affect models routed to the network", async () =>
                {
                    using (ListenerRoutingHarness listener = new ListenerRoutingHarness(settings => settings.Providers.Add(ListenerRoutingHarness.Provider("ollama", "Ollama", "http://127.0.0.1:" + FakeProviderServer.GetFreePort(), "llama3.1:8b"))))
                    using (HttpResponseMessage response = await PostAsync(listener, "/v1/chat/completions", "{\"model\":\"deepseek-r1:671b\",\"messages\":[]}").ConfigureAwait(false))
                    {
                        Check((int)response.StatusCode == 200 && Header(response, "X-Xeno-Route") == "network", "Network route failed because a local provider is down.");
                        Check(!listener.TunnelRequests.Single().ExcludeRequestingClient, "A normal network request excluded this client.");
                    }
                }),
                Case(suite, "ModelsListMergesLocalAndNetwork", "GET /v1/models lists local models first and adds network models this machine does not serve", async () =>
                {
                    using (FakeProviderServer ollama = new FakeProviderServer(request => Tags("llama3.1:8b", "gpt-oss:120b-cloud")))
                    using (ListenerRoutingHarness listener = new ListenerRoutingHarness(settings => settings.Providers.Add(ListenerRoutingHarness.Provider("ollama", "Ollama", ollama.BaseUrl, "llama3.1:8b"))))
                    {
                        listener.TunnelResponder = request => new ServerLocalInferenceResponseMessage { Success = true, StatusCode = 200, ContentType = "application/json", Body = "{\"object\":\"list\",\"data\":[{\"id\":\"llama3.1:8b\",\"object\":\"model\",\"owned_by\":\"someone\"},{\"id\":\"deepseek-r1:671b\",\"object\":\"model\",\"owned_by\":\"xeno\"}]}" };
                        await listener.Service.Backends.CheckNowAsync(CancellationToken.None).ConfigureAwait(false);
                        LocalOpenAiModelList? list = await GetJsonAsync<LocalOpenAiModelList>(listener, "/v1/models").ConfigureAwait(false);
                        List<string> ids = list!.Data.Select(item => item.Id).ToList();
                        Check(ids.SequenceEqual(new[] { "llama3.1:8b", "gpt-oss:120b-cloud", "deepseek-r1:671b" }), "Merged list is wrong: " + String.Join(", ", ids));
                        Check(list.Data[0].OwnedBy == "ollama", "Local entry did not win the duplicate.");
                    }
                }),
                Case(suite, "TagsListDedupesLocalWins", "GET /api/tags merges live local tags with network tags, local first", async () =>
                {
                    using (FakeProviderServer ollama = new FakeProviderServer(request => Tags("llama3.1:8b")))
                    using (ListenerRoutingHarness listener = new ListenerRoutingHarness(settings => settings.Providers.Add(ListenerRoutingHarness.Provider("ollama", "Ollama", ollama.BaseUrl, "llama3.1:8b"))))
                    {
                        listener.TunnelResponder = request => new ServerLocalInferenceResponseMessage { Success = true, StatusCode = 200, ContentType = "application/json", Body = "{\"models\":[{\"name\":\"llama3.1:8b\",\"size\":0},{\"name\":\"qwen3:235b\",\"size\":0}]}" };
                        LocalOllamaTagsResponse? tags = await GetJsonAsync<LocalOllamaTagsResponse>(listener, "/api/tags").ConfigureAwait(false);
                        Check(tags!.Models.Count == 2 && tags.Models[0].Size == 4661224676 && tags.Models[1].Name == "qwen3:235b", "Merged tags are wrong.");
                    }
                }),
                Case(suite, "PsListsLocalOnly", "GET /api/ps lists only models loaded on this machine's providers", async () =>
                {
                    using (FakeProviderServer ollama = new FakeProviderServer(request => request.Path == "/api/ps" ? FakeProviderResponse.Json("{\"models\":[{\"name\":\"llama3.1:8b\",\"model\":\"llama3.1:8b\",\"size\":5000,\"size_vram\":5000}]}") : Tags("llama3.1:8b")))
                    using (ListenerRoutingHarness listener = new ListenerRoutingHarness(settings => settings.Providers.Add(ListenerRoutingHarness.Provider("ollama", "Ollama", ollama.BaseUrl, "llama3.1:8b"))))
                    {
                        LocalOllamaPsResponse? ps = await GetJsonAsync<LocalOllamaPsResponse>(listener, "/api/ps").ConfigureAwait(false);
                        Check(ps!.Models.Count == 1 && ps.Models[0].SizeVram == 5000, "Local ps list is wrong.");
                        Check(listener.TunnelRequests.IsEmpty, "/api/ps asked the network.");
                    }
                }),
                Case(suite, "ShowForwardsToLocalOllama", "POST /api/show for a local model is answered by the local Ollama", async () =>
                {
                    using (FakeProviderServer ollama = new FakeProviderServer(request => request.Path == "/api/show" ? FakeProviderResponse.Json("{\"modelfile\":\"FROM llama3.1\"}") : Tags("llama3.1:8b")))
                    using (ListenerRoutingHarness listener = new ListenerRoutingHarness(settings => settings.Providers.Add(ListenerRoutingHarness.Provider("ollama", "Ollama", ollama.BaseUrl, "llama3.1:8b"))))
                    using (HttpResponseMessage response = await PostAsync(listener, "/api/show", "{\"model\":\"llama3.1:8b\"}").ConfigureAwait(false))
                    {
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        Check(body.Contains("FROM llama3.1", StringComparison.Ordinal) && listener.TunnelRequests.IsEmpty, "Show was not answered locally.");
                    }
                }),
                Case(suite, "ListingsSurviveTunnelOutage", "Model lists still return local models when the tunnel is down", async () =>
                {
                    using (FakeProviderServer ollama = new FakeProviderServer(request => Tags("llama3.1:8b")))
                    using (ListenerRoutingHarness listener = new ListenerRoutingHarness(settings => settings.Providers.Add(ListenerRoutingHarness.Provider("ollama", "Ollama", ollama.BaseUrl, "llama3.1:8b")), tunnel: false))
                    {
                        using (HttpResponseMessage models = await listener.Http.GetAsync(listener.Url("/v1/models")).ConfigureAwait(false))
                        {
                            Check((int)models.StatusCode == 200 && Header(models, "X-Xeno-Route") == "local", "/v1/models failed without a tunnel.");
                        }

                        using (HttpResponseMessage tags = await listener.Http.GetAsync(listener.Url("/api/tags")).ConfigureAwait(false))
                        {
                            Check((int)tags.StatusCode == 200, "/api/tags failed without a tunnel.");
                        }
                    }
                }),
                Case(suite, "ListingsReturn502WhenEverythingDown", "Model lists return a provider-shaped 502 only when no provider and no tunnel answer", async () =>
                {
                    using (ListenerRoutingHarness listener = new ListenerRoutingHarness(settings => settings.Providers.Add(ListenerRoutingHarness.Provider("ollama", "Ollama", "http://127.0.0.1:" + FakeProviderServer.GetFreePort())), tunnel: false))
                    {
                        using (HttpResponseMessage models = await listener.Http.GetAsync(listener.Url("/v1/models")).ConfigureAwait(false))
                        {
                            Check((int)models.StatusCode == 502, "/v1/models did not return 502.");
                        }

                        using (HttpResponseMessage tags = await listener.Http.GetAsync(listener.Url("/api/tags")).ConfigureAwait(false))
                        {
                            string body = await tags.Content.ReadAsStringAsync().ConfigureAwait(false);
                            Check((int)tags.StatusCode == 502 && body.Contains("\"error\"", StringComparison.Ordinal), "/api/tags did not return a provider-shaped 502.");
                        }
                    }
                }),
                Case(suite, "OllamaShapeToVllmReturnsGuidance400", "An Ollama-shaped request for a vLLM model explains to use /v1", async () =>
                {
                    using (FakeProviderServer vllm = new FakeProviderServer(request => Models("mistral-small")))
                    using (ListenerRoutingHarness listener = new ListenerRoutingHarness(settings => settings.Providers.Add(ListenerRoutingHarness.Provider("vllm", "vLLM", vllm.BaseUrl, "mistral-small"))))
                    using (HttpResponseMessage response = await PostAsync(listener, "/api/chat", "{\"model\":\"mistral-small\",\"messages\":[]}").ConfigureAwait(false))
                    {
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        Check((int)response.StatusCode == 400 && body.Contains("/v1", StringComparison.Ordinal), "Guidance 400 is wrong: " + body);
                        Check(!vllm.Requests.Any(item => item.Path == "/api/chat"), "The Ollama-shaped request reached vLLM.");
                    }
                }),
                Case(suite, "LocalOnlyModeReturns404ForNetworkModels", "LocalOnly mode answers unknown models with a 404 and never uses the tunnel", async () =>
                {
                    using (ListenerRoutingHarness listener = new ListenerRoutingHarness(settings => settings.LocalInferenceServer.RoutingMode = LocalInferenceRoutingModeEnum.LocalOnly))
                    using (HttpResponseMessage response = await PostAsync(listener, "/v1/chat/completions", "{\"model\":\"deepseek-r1:671b\",\"messages\":[]}").ConfigureAwait(false))
                    {
                        Check((int)response.StatusCode == 404 && listener.TunnelRequests.IsEmpty, "LocalOnly mode used the network or returned the wrong status.");
                    }
                }),
                Case(suite, "LocalRoutingNeverLogsPromptContent", "Routing writes metadata only, never prompt content", async () =>
                {
                    const string sentinel = "PROMPT-SENTINEL-7f3a9c";
                    using (FakeProviderServer ollama = new FakeProviderServer(request => request.Path == "/api/generate" ? FakeProviderResponse.Json("{\"response\":\"" + sentinel + "-reply\",\"done\":true}") : Tags("llama3.1:8b")))
                    using (ListenerRoutingHarness listener = new ListenerRoutingHarness(settings => settings.Providers.Add(ListenerRoutingHarness.Provider("ollama", "Ollama", ollama.BaseUrl, "llama3.1:8b"))))
                    {
                        TextWriter previousOut = Console.Out;
                        TextWriter previousError = Console.Error;
                        StringWriter captured = new StringWriter();
                        Console.SetOut(captured);
                        Console.SetError(captured);
                        try
                        {
                            using (HttpResponseMessage local = await PostAsync(listener, "/api/generate", "{\"model\":\"llama3.1:8b\",\"prompt\":\"" + sentinel + "\",\"stream\":false}").ConfigureAwait(false)) { }
                            using (HttpResponseMessage network = await PostAsync(listener, "/api/generate", "{\"model\":\"remote-model\",\"prompt\":\"" + sentinel + "\",\"stream\":false}").ConfigureAwait(false)) { }
                        }
                        finally
                        {
                            Console.SetOut(previousOut);
                            Console.SetError(previousError);
                        }

                        string log = captured.ToString();
                        Check(log.Contains("Routing local", StringComparison.Ordinal), "Routing metadata was not logged.");
                        Check(!log.Contains(sentinel, StringComparison.Ordinal), "Prompt or response content appeared in the log.");
                    }
                })
            });
        }

        #endregion

        #region Private-Methods

        private static TestCaseDescriptor Case(string suite, string id, string name, Func<Task> body)
        {
            return new TestCaseDescriptor(suite, id, name, executeAsync: async delegate
            {
                await body().ConfigureAwait(false);
            });
        }

        private static FakeProviderResponse Tags(params string[] models)
        {
            string entries = String.Join(",", models.Select(item => "{\"name\":\"" + item + "\",\"model\":\"" + item + "\",\"size\":4661224676,\"details\":{\"family\":\"llama\"}}"));
            return FakeProviderResponse.Json("{\"models\":[" + entries + "]}");
        }

        private static FakeProviderResponse Models(params string[] models)
        {
            string entries = String.Join(",", models.Select(item => "{\"id\":\"" + item + "\",\"object\":\"model\"}"));
            return FakeProviderResponse.Json("{\"object\":\"list\",\"data\":[" + entries + "]}");
        }

        private static async Task<HttpResponseMessage> PostAsync(ListenerRoutingHarness listener, string path, string body)
        {
            return await listener.Http.PostAsync(listener.Url(path), new StringContent(body, Encoding.UTF8, "application/json")).ConfigureAwait(false);
        }

        private static async Task<T?> GetJsonAsync<T>(ListenerRoutingHarness listener, string path) where T : class
        {
            using (HttpResponseMessage response = await listener.Http.GetAsync(listener.Url(path)).ConfigureAwait(false))
            {
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if ((int)response.StatusCode != 200) throw new InvalidOperationException(path + " returned " + (int)response.StatusCode + ": " + body);
                return Xeno.Core.Serialization.JsonUtil.Deserialize<T>(body);
            }
        }

        private static async Task<string> ReadAllTolerantAsync(HttpResponseMessage response)
        {
            StringBuilder text = new StringBuilder();
            try
            {
                using (Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    char[] buffer = new char[256];
                    int read;
                    while ((read = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0) text.Append(buffer, 0, read);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is HttpRequestException)
            {
            }

            return text.ToString();
        }

        private static string Header(HttpResponseMessage response, string name)
        {
            if (response.Headers.TryGetValues(name, out IEnumerable<string>? values)) return String.Join(",", values);
            if (response.Content.Headers.TryGetValues(name, out IEnumerable<string>? contentValues)) return String.Join(",", contentValues);
            return String.Empty;
        }

        private static async Task<bool> WaitAsync(Func<bool> condition, int timeoutMs)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return true;
                await Task.Delay(50).ConfigureAwait(false);
            }

            return condition();
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        #endregion
    }
}
