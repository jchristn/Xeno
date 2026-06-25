# Testing the Xeno Python SDK

Run the SDK tests:

```bash
cd src/sdk/python
python -m unittest discover -s tests
```

Run the interactive console after installing the package:

```bash
python -m pip install -e .
python -m xeno_client.console
```
