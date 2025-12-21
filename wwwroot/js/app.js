// Helper functions for MindLog journal application

// Get element by ID and return it for JS interop
window.getElement = function(id) {
    console.log(`Getting element with id: ${id}`);
    const element = document.getElementById(id);
    console.log(`Element found: ${element ? 'yes' : 'no'}`);
    if (!element) return null;
    
    // Return a wrapper object that Blazor can use as JSObjectReference
    return {
        getSelectionStart: function() {
            return element.selectionStart || 0;
        },
        getSelectionEnd: function() {
            return element.selectionEnd || 0;
        },
        setSelectionRange: function(start, end) {
            element.setSelectionRange(start, end);
            element.focus();
        },
        invokeAsync: function(method, ...args) {
            return new Promise((resolve, reject) => {
                try {
                    if (method === 'getSelectionStart') {
                        resolve(element.selectionStart || 0);
                    } else if (method === 'getSelectionEnd') {
                        resolve(element.selectionEnd || 0);
                    } else if (method === 'setSelectionRange') {
                        element.setSelectionRange(args[0], args[1]);
                        element.focus();
                        resolve();
                    } else {
                        reject(new Error(`Method ${method} not supported`));
                    }
                } catch (error) {
                    reject(error);
                }
            });
        }
    };
};

// Convert Markdown to HTML
window.markdownToHtml = function(markdown) {
    console.log('Converting markdown to HTML, length:', markdown ? markdown.length : 0);
    if (!markdown) return "";
    
    var html = markdown;
    
    // Headers
    html = html.replace(/^### (.*)$/gm, '<h3>$1</h3>');
    html = html.replace(/^## (.*)$/gm, '<h2>$1</h2>');
    html = html.replace(/^# (.*)$/gm, '<h1>$1</h1>');
    
    // Bold and italic
    html = html.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
    html = html.replace(/\*(.*?)\*/g, '<em>$1</em>');
    
    // Code
    html = html.replace(/`([^`]+)`/g, '<code>$1</code>');
    
    // Links
    html = html.replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2" target="_blank">$1</a>');
    
    // Line breaks
    html = html.replace(/\n/g, '<br>');
    
    return html;
};

// Theme switching function
window.switchTheme = function(theme) {
    console.log('Switching theme to:', theme);
    const htmlElement = document.documentElement;
    
    // Set data attribute
    htmlElement.setAttribute('data-theme', theme);
    
    // Set class
    if (theme === 'dark') {
        htmlElement.classList.add('dark-theme');
    } else {
        htmlElement.classList.remove('dark-theme');
    }
    
    // Save to localStorage
    localStorage.setItem('theme', theme);
    
    console.log('Theme switched successfully');
    console.log('data-theme:', htmlElement.getAttribute('data-theme'));
    console.log('dark-theme class:', htmlElement.classList.contains('dark-theme'));
};