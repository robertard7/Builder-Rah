import React from 'react';

function CodeSnippet({ children }) {
  return (
    <pre className="code-preview" style={{ background: '#0f172a', color: '#e2e8f0' }}>
      {children}
    </pre>
  );
}

function Docs() {
  return (
    <div>
      <h2>Provider API Documentation</h2>
      <p>Use these endpoints to interact with the provider backend. All calls are proxied via Vite during development.</p>

      <h3>POST /api/jobs</h3>
      <p>Create a new job. When no session is provided, a new session token is issued.</p>
      <CodeSnippet>{`curl -X POST http://localhost:5173/api/jobs
curl -X POST "http://localhost:5173/api/jobs?session=SESSION_TOKEN&text=Run+task"`}</CodeSnippet>

      <h3>GET /api/plan</h3>
      <p>Fetch the current plan for a session.</p>
      <CodeSnippet>{`curl "http://localhost:5173/api/plan?session=SESSION_TOKEN"`}</CodeSnippet>

      <h3>POST /api/plan/update</h3>
      <p>Update the plan for a session.</p>
      <CodeSnippet>{`curl -X POST "http://localhost:5173/api/plan/update?session=SESSION_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"steps":[{"id":"1","text":"Do thing"}]}'`}</CodeSnippet>

      <h3>GET /api/output</h3>
      <p>Retrieve accumulated output cards for a session.</p>
      <CodeSnippet>{`curl "http://localhost:5173/api/output?session=SESSION_TOKEN"`}</CodeSnippet>

      <h3>GET /api/stream</h3>
      <p>Stream output cards in real time via Server-Sent Events.</p>
      <CodeSnippet>{`const source = new EventSource("/api/stream?session=SESSION_TOKEN");
source.onmessage = (event) => {
  const card = JSON.parse(event.data);
  console.log(card);
};`}</CodeSnippet>

      <h3>GET /api/artifacts</h3>
      <p>Retrieve the artifact tree for the session.</p>
      <CodeSnippet>{`curl "http://localhost:5173/api/artifacts?session=SESSION_TOKEN"`}</CodeSnippet>

      <h3>GET /api/artifacts/download</h3>
      <p>Download a ZIP archive of generated artifacts.</p>
      <CodeSnippet>{`curl -o artifacts.zip "http://localhost:5173/api/artifacts/download?session=SESSION_TOKEN"`}</CodeSnippet>

      <h3>POST /api/session/reset</h3>
      <p>Reset the current session state.</p>
      <CodeSnippet>{`curl -X POST "http://localhost:5173/api/session/reset?session=SESSION_TOKEN"`}</CodeSnippet>
    </div>
  );
}

export default Docs;
