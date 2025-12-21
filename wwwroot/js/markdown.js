function markdownToHtml(markdown) {
    if (!markdown) return '';
    
    let html = markdown;
    
    html = html.replace(/^### (.*$)/gim, '<h3>$1</h3>');
    html = html.replace(/^## (.*$)/gim, '<h2>$1</h2>');
    html = html.replace(/^# (.*$)/gim, '<h1>$1</h1>');
    
    html = html.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
    html = html.replace(/\*(.*?)\*/g, '<em>$1</em>');
    
    html = html.replace(/`([^`]+)`/g, '<code>$1</code>');
    
    const codeBlockRegex = /```([\s\S]*?)```/g;
    html = html.replace(codeBlockRegex, '<pre><code>$1</code></pre>');
    
    html = html.replace(/^\- (.*)$/gim, '<li>$1</li>');
    html = html.replace(/(<li>.*<\/li>\s*)+/g, '<ul>$&</ul>');
    
    html = html.replace(/^\d+\. (.*)$/gim, '<li>$1</li>');
    html = html.replace(/(<li>.*<\/li>\s*)+/g, '<ol>$&</ol>');
    
    html = html.replace(/^> (.*)$/gim, '<blockquote>$1</blockquote>');
    
    html = html.replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2" target="_blank">$1</a>');
    
    html = html.replace(/\n/g, '<br>');
    
    return html;
}

function getElement(selector) {
    return document.querySelector(selector);
}