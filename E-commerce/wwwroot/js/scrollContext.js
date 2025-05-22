document.addEventListener("DOMContentLoaded", function () {
    const navbarTitle = document.querySelector(".navbar-title");

    function handleScroll() {
        if (window.scrollY > 10) {
            navbarTitle.classList.add("visible");
        } else {
            navbarTitle.classList.remove("visible");
        }
    }

    window.addEventListener("scroll", handleScroll);
});
