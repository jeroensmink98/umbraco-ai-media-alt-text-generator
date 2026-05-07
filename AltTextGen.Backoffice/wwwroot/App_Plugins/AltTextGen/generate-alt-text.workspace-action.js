import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import {
  UMB_WORKSPACE_CONTEXT,
  UmbWorkspaceActionBase,
} from "@umbraco-cms/backoffice/workspace";

console.log("[AltTextGen] workspace-action module loaded");

class GenerateAltTextWorkspaceAction extends UmbWorkspaceActionBase {
  #workspaceContext;
  #notificationContext;
  #authContext;

  constructor(host, args) {
    super(host, args);
    console.log("[AltTextGen] GenerateAltTextWorkspaceAction constructed", {
      args,
    });
    this.disable();

    this.consumeContext(UMB_WORKSPACE_CONTEXT, (workspaceContext) => {
      this.#workspaceContext = workspaceContext;
      this.observe(
        workspaceContext?.unique,
        (unique) => (unique ? this.enable() : this.disable()),
        "altTextGenerationWorkspaceUniqueObserver",
      );
    });

    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (notificationContext) => {
      this.#notificationContext = notificationContext;
    });

    this.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
      this.#authContext = authContext;
    });
  }

  async execute() {
    console.log("[AltTextGen] GenerateAltTextWorkspaceAction execute()");
    const mediaKey = this.#workspaceContext?.getUnique?.();

    if (!mediaKey) {
      throw new Error("The current media item has not been saved yet.");
    }

    const accessToken = await this.#authContext?.getLatestToken?.();

    const response = await fetch("/umbraco/backoffice/api/alt-text-generation/generate", {
      method: "POST",
      credentials: "same-origin",
      headers: {
        "Content-Type": "application/json",
        ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      },
      body: JSON.stringify({
        mediaKey,
        overwrite: true,
      }),
    });

    const responseBody = await readResponseBody(response);

    if (!response.ok) {
      const message =
        responseBody?.detail ||
        responseBody?.title ||
        responseBody ||
        "Unable to generate alt text.";
      this.#notify("danger", "Alt text generation failed", message);
      throw new Error(message);
    }

    await this.#workspaceContext?.reload?.();

    this.#notify(
      "positive",
      "Alt text generated",
      responseBody?.altText || "The media item alt text was updated.",
    );
  }

  #notify(color, headline, message) {
    this.#notificationContext?.peek(color, {
      data: {
        headline,
        message,
      },
    });
  }
}

async function readResponseBody(response) {
  const contentType = response.headers.get("content-type") || "";

  if (contentType.includes("application/json")) {
    return response.json();
  }

  return response.text();
}

export { GenerateAltTextWorkspaceAction as api };
