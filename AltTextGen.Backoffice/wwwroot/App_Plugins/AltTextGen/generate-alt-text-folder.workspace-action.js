import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import {
  UMB_WORKSPACE_CONTEXT,
  UmbWorkspaceActionBase,
} from "@umbraco-cms/backoffice/workspace";

const POLL_INTERVAL_MS = 2000;

class GenerateAltTextFolderWorkspaceAction extends UmbWorkspaceActionBase {
  #workspaceContext;
  #notificationContext;
  #authContext;

  constructor(host, args) {
    super(host, args);

    this.consumeContext(UMB_WORKSPACE_CONTEXT, (workspaceContext) => {
      this.#workspaceContext = workspaceContext;
    });

    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (notificationContext) => {
      this.#notificationContext = notificationContext;
    });

    this.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
      this.#authContext = authContext;
    });
  }

  async execute() {
    const parentMediaKey = this.#workspaceContext?.getUnique?.();

    if (!parentMediaKey) {
      throw new Error("The current folder has not been saved yet.");
    }

    const accessToken = await this.#authContext?.getLatestToken?.();
    const headers = {
      "Content-Type": "application/json",
      ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
    };

    const startResponse = await fetch(
      "/umbraco/backoffice/api/alt-text-generation/generate-under-folder",
      {
        method: "POST",
        credentials: "same-origin",
        headers,
        body: JSON.stringify({
          parentMediaKey,
          overwrite: true,
        }),
      },
    );

    const startBody = await readResponseBody(startResponse);
    if (!startResponse.ok) {
      const message =
        startBody?.detail ||
        startBody?.title ||
        startBody ||
        "Unable to queue folder alt text generation.";
      this.#notify("danger", "Batch generation failed", message);
      throw new Error(message);
    }

    this.#notify(
      "positive",
      "Batch generation started",
      `Queued ${startBody?.totalItemsQueued ?? 0} image item(s) from this folder.`,
    );

    const jobId = startBody?.jobId;
    if (!jobId) {
      return;
    }

    await this.#pollUntilFinished(jobId, headers);
  }

  async #pollUntilFinished(jobId, headers) {
    let finished = false;

    while (!finished) {
      await sleep(POLL_INTERVAL_MS);

      const statusResponse = await fetch(
        `/umbraco/backoffice/api/alt-text-generation/jobs/${jobId}`,
        {
          method: "GET",
          credentials: "same-origin",
          headers,
        },
      );

      const statusBody = await readResponseBody(statusResponse);
      if (!statusResponse.ok) {
        const message =
          statusBody?.detail ||
          statusBody?.title ||
          statusBody ||
          "Unable to read batch job status.";
        this.#notify("danger", "Batch generation failed", message);
        throw new Error(message);
      }

      const status = statusBody?.status;
      if (status === "Completed" || status === "Failed") {
        finished = true;
        const summary = `Processed ${statusBody?.processedItems ?? 0}/${statusBody?.totalItems ?? 0}. Success: ${statusBody?.succeededItems ?? 0}, Skipped: ${statusBody?.skippedItems ?? 0}, Failed: ${statusBody?.failedItems ?? 0}.`;

        if (status === "Completed") {
          this.#notify("positive", "Folder batch completed", summary);
        } else {
          this.#notify(
            "danger",
            "Folder batch failed",
            `${summary} ${statusBody?.errorMessage || ""}`.trim(),
          );
        }
      }
    }
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

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function readResponseBody(response) {
  const contentType = response.headers.get("content-type") || "";

  if (contentType.includes("application/json")) {
    return response.json();
  }

  return response.text();
}

export { GenerateAltTextFolderWorkspaceAction as api };
