# Testing the Xeno Python SDK

Run the SDK tests:

```bash
cd src/sdk/python
python -m unittest discover -s tests
```

The test suite verifies request construction for management APIs, OpenAI-compatible APIs, request history, and server-collected client telemetry routes.

Run the interactive console after installing the package:

```bash
python -m pip install -e .
python -m xeno_client.console
```
