# Workflow Spec Recorder

Workflow Spec Recorder helps users turn browser workflow examples and business source materials into automation-ready requirement documents. APA / AI-RPA is the first built-in delivery template, not the product boundary.

## Language

**LLM Configuration**:
A local set of provider settings used when generating generalized APA requirements: provider name, base URL, model, and API key. It is saved on the user's machine and is separate from browser capture settings.
_Avoid_: API settings, model settings

**API Key**:
The secret token inside an **LLM Configuration** that authorizes calls to the selected OpenAI-compatible provider. In this product, it is not a browser extension credential or an APA runtime credential.
_Avoid_: Extension key, APA key

**Connection Test**:
A user-triggered check that verifies whether the current **LLM Configuration** can reach the configured provider and model. It is separate from the browser extension's desktop connection status.
_Avoid_: Health check, extension status

**Browser Extension Connection**:
The localhost connection between the Chrome extension and the desktop recorder's local capture server. It indicates whether browser events can reach the recorder, not whether LLM generation is configured.
_Avoid_: LLM connection, API connection

## Example Dialogue

Developer: "When the user clicks Test Connection, should we check the Chrome extension?"

Domain expert: "No. Test Connection means the LLM Configuration can call the model provider. The Chrome extension has its own Browser Extension Connection status."

Developer: "If the API Key is saved, is it saved for APA runtime execution?"

Domain expert: "No. The API Key belongs only to LLM Configuration for generating generalized requirements."
