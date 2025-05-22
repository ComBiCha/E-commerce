$(function () {

    if ($("a.confirmDeletion").length) {
        $("a.confirmDeletion").click(() => {
            if (!confirm("Confirm deletion")) return false;
        });
    }

    if ($("div.alert.notification").length) {
        setTimeout(() => {
            $("div.alert.notification").fadeOut();
        }, 2000);
    }

});

/*function readURL(input, imgPreviewId) {
    if (input.files && input.files[0]) {
        let reader = new FileReader();
        reader.onload = function (e) {
            $("#" + imgPreviewId).attr("src", e.target.result).width(200).height(200).show();
        };
        reader.readAsDataURL(input.files[0]);
    }
}*/