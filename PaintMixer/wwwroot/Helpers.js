export var Helpers = {

    ShowAlert: async (title, message, icon = null, footer = null, confirmButtonText = null) => { // POSSIBLE ICONS: success, error, warning, info, question

        //console.log(title, message, icon, footer);

        Swal.fire({
            icon: (icon == null) ? 'success' : icon,
            title: title,
            text: message,
            footer: footer,
            confirmButtonColor: '#0d6efd',
            confirmButtonText: (confirmButtonText == null) ? "Dismiss" : confirmButtonText,
        });

    },

};