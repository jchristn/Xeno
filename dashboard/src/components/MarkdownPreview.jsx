import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';

export default function MarkdownPreview({ text }) {
  return (
    <div className="api-explorer-markdown">
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        components={{
          a: ({ children, href }) => (
            <a href={href} rel="noreferrer" target="_blank">{children}</a>
          )
        }}
      >
        {text}
      </ReactMarkdown>
    </div>
  );
}
