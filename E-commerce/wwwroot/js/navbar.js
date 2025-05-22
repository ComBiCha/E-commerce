window.addEventListener("scroll", function () {
    const navbarTitle = document.querySelector(".navbar-title");
    if (window.scrollY > 50) {
        navbarTitle.classList.add("visible");
    } else {
        navbarTitle.classList.remove("visible");
    }
});
